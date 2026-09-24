using System.Data;
using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using LwWinApp.Core;
using LwWinApp.Utils;

namespace LwWinApp.Data;

/// <summary>MySQL 连接工厂实现，基于 MySqlConnector + Dapper。</summary>
public sealed class MySqlDatabaseFactory : IDatabaseFactory
{
    /// <summary>超过这个耗时就在日志里留一条记录，方便定位慢查询。</summary>
    private const int SlowQueryThresholdMs = 500;

    private readonly ILogger<MySqlDatabaseFactory> _logger;
    private readonly MySqlConnectionStringBuilder _builder;

    /// <summary>
    /// 打开下划线列名到 PascalCase 属性的映射，必须在任何查询之前生效。
    ///
    /// Dapper 默认只做大小写不敏感匹配，所以 <c>user_name</c> 不会映射到 <c>UserName</c>：
    /// 单字列（email、phone、gender、status）看着正常，
    /// 多字列（user_name、real_name、create_time、dept_id）会静默留空 ——
    /// 这种「一半列有值一半是空的」最难查，所以在这里一次性配好。
    ///
    /// 放在静态构造函数里：它是全局开关，而使用数据层必然先构造这个类型。
    /// </summary>
    static MySqlDatabaseFactory()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public MySqlDatabaseFactory(IConfiguration configuration, ILogger<MySqlDatabaseFactory> logger)
    {
        _logger = logger;

        var connectionString = configuration.GetConnectionString(AppConstants.DefaultConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"appsettings.json 中没有找到 ConnectionStrings:{AppConstants.DefaultConnectionName}。");
        }

        _builder = new MySqlConnectionStringBuilder(connectionString);
        ConnectionString = _builder.ConnectionString;

        _logger.LogInformation("数据库目标已配置：{Target}", DescribeTarget().Display);
    }

    public string ConnectionString { get; }

    public MySqlConnection CreateConnection() => new(ConnectionString);

    public async Task<MySqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = CreateConnection();
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            // 打开失败时把连接释放掉，否则会泄漏到池外。
            await connection.DisposeAsync();
            throw;
        }
    }

    public Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken ct = default)
        => RunAsync(async conn =>
        {
            var cmd = new CommandDefinition(sql, param, cancellationToken: ct);
            return await conn.ExecuteAsync(cmd);
        }, sql, ct);

    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken ct = default)
        => RunAsync<IReadOnlyList<T>>(async conn =>
        {
            var cmd = new CommandDefinition(sql, param, cancellationToken: ct);
            var rows = await conn.QueryAsync<T>(cmd);
            return rows.AsList();
        }, sql, ct);

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken ct = default)
        => RunAsync(async conn =>
        {
            var cmd = new CommandDefinition(sql, param, cancellationToken: ct);
            return await conn.QueryFirstOrDefaultAsync<T>(cmd);
        }, sql, ct);

    public Task<DataTable> QueryDataTableAsync(string sql, object? param = null, CancellationToken ct = default)
        => RunAsync(async conn =>
        {
            var cmd = new CommandDefinition(sql, param, cancellationToken: ct);
            await using var reader = await conn.ExecuteReaderAsync(cmd);
            var table = new DataTable();
            table.Load(reader);
            return table;
        }, sql, ct);

    public async Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(ct);
            var version = await conn.ExecuteScalarAsync<string>(
                new CommandDefinition("SELECT VERSION();", cancellationToken: ct));
            var tableCount = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    "SELECT COUNT(1) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE();",
                    cancellationToken: ct));

            return (true, $"连接成功 · MySQL {version} · 当前库 {_builder.Database} 共 {tableCount} 张表");
        }
        catch (OperationCanceledException)
        {
            return (false, "连接测试已取消");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "数据库连通性测试失败");
            return (false, $"连接失败：{ex.Message}");
        }
    }

    public DatabaseTarget DescribeTarget() => new(
        _builder.Server,
        _builder.Port.ToString(),
        _builder.Database,
        _builder.UserID);

    /// <summary>
    /// 统一的「开连接 → 执行 → 释放」包装，顺带记录慢查询。
    /// </summary>
    private async Task<TResult> RunAsync<TResult>(Func<MySqlConnection, Task<TResult>> action, string sql, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        await using var conn = await OpenConnectionAsync(ct);
        var result = await action(conn);
        sw.Stop();

        if (sw.ElapsedMilliseconds >= SlowQueryThresholdMs)
        {
            _logger.LogWarning("慢查询 {Elapsed}ms：{Sql}", sw.ElapsedMilliseconds, Summarize(sql));
        }
        else
        {
            _logger.LogDebug("查询完成 {Elapsed}ms：{Sql}", sw.ElapsedMilliseconds, Summarize(sql));
        }

        return result;
    }

    /// <summary>日志里不要把整条长 SQL 全打出来，压成一行并截断。</summary>
    private static string Summarize(string sql)
        => string.Join(' ', sql.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
                 .Trim()
                 .Truncate(300);
}
