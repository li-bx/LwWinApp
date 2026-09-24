using System.Data;
using MySqlConnector;

namespace LwWinApp.Data;

/// <summary>基于 MySqlConnector 的事务实现。</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IDatabaseFactory _db;
    private readonly ILogger<UnitOfWork> _logger;

    private MySqlConnection? _connection;
    private MySqlTransaction? _transaction;

    /// <summary>嵌套深度。只有最外层负责提交/回滚，内层套用同一个事务。</summary>
    private int _depth;

    public UnitOfWork(IDatabaseFactory db, ILogger<UnitOfWork> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IDbTransaction? Transaction => _transaction;

    public bool HasActiveTransaction => _transaction is not null;

    public async Task<IDbTransaction> BeginAsync(CancellationToken ct = default)
    {
        if (_transaction is not null) return _transaction;

        _connection = await _db.OpenConnectionAsync(ct);
        try
        {
            _transaction = await _connection.BeginTransactionAsync(ct);
            _logger.LogDebug("事务已开启");
            return _transaction;
        }
        catch
        {
            await _connection.DisposeAsync();
            _connection = null;
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;

        try
        {
            await _transaction.CommitAsync(ct);
            _logger.LogDebug("事务已提交");
        }
        finally
        {
            await ReleaseAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;

        try
        {
            await _transaction.RollbackAsync(ct);
            _logger.LogDebug("事务已回滚");
        }
        catch (Exception ex)
        {
            // 回滚本身失败通常意味着连接已经断了，记下来但不要再往上抛，
            // 否则会把真正的业务异常掩盖掉。
            _logger.LogError(ex, "事务回滚失败");
        }
        finally
        {
            await ReleaseAsync();
        }
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<IDbTransaction, Task<TResult>> action, CancellationToken ct = default)
    {
        var isOutermost = _depth == 0;
        _depth++;

        try
        {
            var tx = await BeginAsync(ct);
            var result = await action(tx);

            if (isOutermost) await CommitAsync(ct);
            return result;
        }
        catch
        {
            if (isOutermost) await RollbackAsync(ct);
            throw;
        }
        finally
        {
            _depth--;
        }
    }

    public async Task ExecuteAsync(Func<IDbTransaction, Task> action, CancellationToken ct = default)
        => await ExecuteAsync(async tx =>
        {
            await action(tx);
            return true;
        }, ct);

    public async ValueTask DisposeAsync()
    {
        // 没提交就释放，说明调用方漏了或者中途异常，回滚掉避免留下悬挂事务。
        if (_transaction is not null)
        {
            _logger.LogWarning("工作单元在未提交的情况下被释放，已自动回滚");
            await RollbackAsync();
        }

        await ReleaseAsync();
    }

    private async Task ReleaseAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
