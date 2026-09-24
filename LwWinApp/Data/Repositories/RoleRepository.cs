using System.Text;
using Dapper;
using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;

namespace LwWinApp.Data.Repositories;

public interface IRoleRepository : IRepository<SysRole>
{
    Task<PagedResult<SysRole>> QueryPagedAsync(RoleQueryFilter filter, CancellationToken ct = default);

    /// <summary>取所有启用状态的角色，供下拉框绑定。</summary>
    Task<IReadOnlyList<SysRole>> GetEnabledListAsync(CancellationToken ct = default);

    Task<bool> IsRoleCodeTakenAsync(string roleCode, long? excludeId = null, CancellationToken ct = default);

    /// <summary>该角色下是否还有用户，删除前校验用。</summary>
    Task<bool> HasUsersAsync(long roleId, CancellationToken ct = default);
}

/// <summary>角色仓储。列表带出每个角色关联的用户数。</summary>
public sealed class RoleRepository : RepositoryBase<SysRole>, IRoleRepository
{
    public RoleRepository(IDatabaseFactory db, ILogger<RoleRepository> logger) : base(db, logger) { }

    protected override string? TableAlias => "r";

    protected override string FromClause => "`sys_role` r";

    /// <summary>关联用户数用相关子查询算，避免 JOIN + GROUP BY 影响分页计数。</summary>
    protected override string SelectClause =>
        "r.*, (SELECT COUNT(1) FROM `sys_user` u WHERE u.`role_id` = r.`id`) AS UserCount";

    public async Task<PagedResult<SysRole>> QueryPagedAsync(RoleQueryFilter filter, CancellationToken ct = default)
    {
        var (where, parameters) = BuildFilter(filter);
        return await base.QueryPagedAsync(filter, where, parameters, "sort", ct);
    }

    public async Task<IReadOnlyList<SysRole>> GetEnabledListAsync(CancellationToken ct = default)
        => await QueryCustomAsync<SysRole>(
            "SELECT `id`, `role_code`, `role_name` FROM `sys_role` WHERE `status` = 1 ORDER BY `sort`, `id`;",
            null, ct);

    public async Task<bool> IsRoleCodeTakenAsync(string roleCode, long? excludeId = null, CancellationToken ct = default)
    {
        var sql = "SELECT COUNT(1) FROM `sys_role` WHERE `role_code` = @roleCode";
        var parameters = new DynamicParameters();
        parameters.Add("roleCode", roleCode);

        if (excludeId is > 0)
        {
            sql += " AND `id` <> @excludeId";
            parameters.Add("excludeId", excludeId.Value);
        }

        return await Db.QueryFirstOrDefaultAsync<long>(sql + ";", parameters, ct) > 0;
    }

    public async Task<bool> HasUsersAsync(long roleId, CancellationToken ct = default)
        => await ScalarAsync<long>(
            "SELECT COUNT(1) FROM `sys_user` WHERE `role_id` = @roleId;",
            new { roleId }, ct: ct) > 0;

    private static (string Where, DynamicParameters Parameters) BuildFilter(RoleQueryFilter filter)
    {
        var sql = new StringBuilder("1 = 1");
        var parameters = new DynamicParameters();

        if (filter.HasKeyword)
        {
            sql.Append(" AND (r.`role_code` LIKE @keyword OR r.`role_name` LIKE @keyword OR r.`remark` LIKE @keyword)");
            parameters.Add("keyword", filter.LikeKeyword);
        }

        if (filter.Status is not null)
        {
            sql.Append(" AND r.`status` = @status");
            parameters.Add("status", (int)filter.Status.Value);
        }

        return (sql.ToString(), parameters);
    }
}
