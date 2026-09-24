using System.Text;
using Dapper;
using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;

namespace LwWinApp.Data.Repositories;

public interface ILogRepository : IRepository<SysLog>
{
    Task<PagedResult<SysLog>> QueryPagedAsync(LogQueryFilter filter, CancellationToken ct = default);

    /// <summary>取最近若干条，仪表盘用。</summary>
    Task<IReadOnlyList<SysLog>> GetRecentAsync(int count = 8, CancellationToken ct = default);

    /// <summary>取出现过的模块名列表，筛选下拉框用。</summary>
    Task<IReadOnlyList<string>> GetModulesAsync(CancellationToken ct = default);

    Task<long> CountSinceAsync(DateTime since, CancellationToken ct = default);

    /// <summary>
    /// 写一条操作日志。
    /// 刻意不抛异常：记日志失败不应该把正常的业务操作一起搞挂。
    /// </summary>
    Task WriteAsync(LogLevelKind level, string module, string message,
        string? userName = null, string? ip = null, CancellationToken ct = default);
}

/// <summary>操作日志仓储。</summary>
public sealed class LogRepository : RepositoryBase<SysLog>, ILogRepository
{
    public LogRepository(IDatabaseFactory db, ILogger<LogRepository> logger) : base(db, logger) { }

    public async Task<PagedResult<SysLog>> QueryPagedAsync(LogQueryFilter filter, CancellationToken ct = default)
    {
        var (where, parameters) = BuildFilter(filter);
        return await base.QueryPagedAsync(filter, where, parameters, "create_time", ct);
    }

    public async Task<IReadOnlyList<SysLog>> GetRecentAsync(int count = 8, CancellationToken ct = default)
        => await QueryCustomAsync<SysLog>(
            "SELECT * FROM `sys_log` ORDER BY `id` DESC LIMIT @count;",
            new { count = Math.Clamp(count, 1, 200) }, ct);

    public async Task<IReadOnlyList<string>> GetModulesAsync(CancellationToken ct = default)
        => await QueryCustomAsync<string>(
            "SELECT DISTINCT `module` FROM `sys_log` WHERE `module` <> '' ORDER BY `module`;",
            null, ct);

    public async Task<long> CountSinceAsync(DateTime since, CancellationToken ct = default)
        => await ScalarAsync<long>(
            "SELECT COUNT(1) FROM `sys_log` WHERE `create_time` >= @since;",
            new { since }, ct: ct);

    public async Task WriteAsync(LogLevelKind level, string module, string message,
        string? userName = null, string? ip = null, CancellationToken ct = default)
    {
        try
        {
            var entry = new SysLog
            {
                Level = level,
                Module = module,
                Message = message,
                UserName = userName,
                Ip = ip,
                CreateTime = DateTime.Now
            };

            await InsertAsync(entry, null, ct);
        }
        catch (Exception ex)
        {
            // 落库失败就退到应用日志，绝不打扰调用方。
            Logger.LogWarning(ex, "写入操作日志失败：{Module} / {Message}", module, message);
        }
    }

    private static (string Where, DynamicParameters Parameters) BuildFilter(LogQueryFilter filter)
    {
        var sql = new StringBuilder("1 = 1");
        var parameters = new DynamicParameters();

        if (filter.HasKeyword)
        {
            sql.Append(" AND (`message` LIKE @keyword OR `user_name` LIKE @keyword OR `ip` LIKE @keyword)");
            parameters.Add("keyword", filter.LikeKeyword);
        }

        if (filter.Level is not null)
        {
            sql.Append(" AND `level` = @level");
            parameters.Add("level", (int)filter.Level.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Module))
        {
            sql.Append(" AND `module` = @module");
            parameters.Add("module", filter.Module);
        }

        if (filter.From is not null)
        {
            sql.Append(" AND `create_time` >= @from");
            parameters.Add("from", filter.From.Value.LocalDateTime);
        }

        if (filter.To is not null)
        {
            // 用户选的是「到某天为止」，含当天，所以取次日零点作为开区间上界。
            sql.Append(" AND `create_time` < @to");
            parameters.Add("to", filter.To.Value.LocalDateTime.Date.AddDays(1));
        }

        return (sql.ToString(), parameters);
    }
}
