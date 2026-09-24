using System.Data;

namespace LwWinApp.Data;

/// <summary>
/// 工作单元。跨多个仓储的事务用这个，不要自己去 BeginTransaction。
///
/// 只在事务真的开启期间占用连接，提交/回滚后立刻释放，
/// 因此它可以安全地作为长生命周期对象注入到服务里。
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>当前进行中的事务，没有则为 null。</summary>
    IDbTransaction? Transaction { get; }

    bool HasActiveTransaction { get; }

    /// <summary>开启事务。已经在事务中时直接复用现有的，实现嵌套语义。</summary>
    Task<IDbTransaction> BeginAsync(CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);

    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>在事务里跑一段逻辑，正常结束自动提交、抛异常自动回滚。</summary>
    Task<TResult> ExecuteAsync<TResult>(Func<IDbTransaction, Task<TResult>> action, CancellationToken ct = default);

    /// <summary>不关心返回值的版本。</summary>
    Task ExecuteAsync(Func<IDbTransaction, Task> action, CancellationToken ct = default);
}
