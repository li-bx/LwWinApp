using System.Data;
using MySqlConnector;

namespace LwWinApp.Data;

/// <summary>目标库信息（不含密码），设置页展示用。</summary>
public sealed record DatabaseTarget(string Server, string Port, string Database, string User)
{
    public string Display => $"{Server}:{Port}/{Database}";
}

/// <summary>
/// 连接工厂。负责从配置读连接串、创建/打开连接。
/// 连接池由 MySqlConnector 自己管，这里不做缓存。
/// </summary>
public interface IDatabaseFactory
{
    string ConnectionString { get; }

    /// <summary>创建未打开的连接。需要自己 using 释放。</summary>
    MySqlConnection CreateConnection();

    /// <summary>打开一个连接。同一个连接内才能用 LAST_INSERT_ID() / 事务。</summary>
    Task<MySqlConnection> OpenConnectionAsync(CancellationToken ct = default);

    // ---- 便捷方法：内部自动开关连接，适合一次性查询，不适合事务 ----

    Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken ct = default);

    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken ct = default);

    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken ct = default);

    /// <summary>通用的 DataTable 查询，用于动态列导出等场景。</summary>
    Task<DataTable> QueryDataTableAsync(string sql, object? param = null, CancellationToken ct = default);

    /// <summary>连通性测试。返回是否成功以及一句可展示的结果说明。</summary>
    Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>解析连接串得到目标库信息（供界面展示，绝不返回密码）。</summary>
    DatabaseTarget DescribeTarget();
}
