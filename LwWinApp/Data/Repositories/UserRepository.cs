using System.Text;
using Dapper;
using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;

namespace LwWinApp.Data.Repositories;

public interface IUserRepository : IRepository<SysUser>
{
    /// <summary>带部门名/角色名的分页查询。</summary>
    Task<PagedResult<SysUser>> QueryPagedAsync(UserQueryFilter filter, CancellationToken ct = default);

    /// <summary>取单个用户详情（含部门名、角色名）。</summary>
    Task<SysUser?> GetDetailAsync(long id, CancellationToken ct = default);

    /// <summary>账号是否已被占用。编辑时用 excludeId 排除自己。</summary>
    Task<bool> IsUserNameTakenAsync(string userName, long? excludeId = null, CancellationToken ct = default);

    /// <summary>取用于下拉选择的最小列表（只查 Id + 姓名）。</summary>
    Task<IReadOnlyList<SysUser>> GetLookupListAsync(CancellationToken ct = default);

    /// <summary>
    /// 按部门统计人数，仪表盘用。
    /// 没有部门的用户归到「未分配」里，否则这部分人在图表上会凭空消失。
    /// </summary>
    Task<IReadOnlyList<NameCount>> GetCountByDeptAsync(int top = 8, CancellationToken ct = default);

    /// <summary>按角色统计人数，仪表盘用。</summary>
    Task<IReadOnlyList<NameCount>> GetCountByRoleAsync(int top = 8, CancellationToken ct = default);
}

/// <summary>
/// 用户仓储。查询走 LEFT JOIN 带出部门名和角色名，
/// 写入仍然只落在 sys_user 一张表上（基类的写方法不受 JOIN 影响）。
/// </summary>
public sealed class UserRepository : RepositoryBase<SysUser>, IUserRepository
{
    /// <summary>JOIN 出来的列也要能排序，显式声明一下。</summary>
    private static readonly IReadOnlySet<string> SortableColumns =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dept_name", "role_name" };

    public UserRepository(IDatabaseFactory db, ILogger<UserRepository> logger) : base(db, logger) { }

    protected override string? TableAlias => "u";

    protected override string FromClause =>
        "`sys_user` u " +
        "LEFT JOIN `sys_dept` d ON u.`dept_id` = d.`id` " +
        "LEFT JOIN `sys_role` r ON u.`role_id` = r.`id`";

    protected override string SelectClause =>
        "u.*, d.`dept_name` AS DeptName, r.`role_name` AS RoleName";

    protected override IReadOnlySet<string> ExtraSortableColumns => SortableColumns;

    public async Task<PagedResult<SysUser>> QueryPagedAsync(UserQueryFilter filter, CancellationToken ct = default)
    {
        var (where, parameters) = BuildFilter(filter);
        return await base.QueryPagedAsync(filter, where, parameters, "create_time", ct);
    }

    public async Task<SysUser?> GetDetailAsync(long id, CancellationToken ct = default)
        => await GetByIdAsync(id, ct);

    public async Task<bool> IsUserNameTakenAsync(string userName, long? excludeId = null, CancellationToken ct = default)
    {
        var sql = "SELECT COUNT(1) FROM `sys_user` WHERE `user_name` = @userName";
        var parameters = new DynamicParameters();
        parameters.Add("userName", userName);

        if (excludeId is > 0)
        {
            sql += " AND `id` <> @excludeId";
            parameters.Add("excludeId", excludeId.Value);
        }

        var count = await Db.QueryFirstOrDefaultAsync<long>(sql + ";", parameters, ct);
        return count > 0;
    }

    public async Task<IReadOnlyList<SysUser>> GetLookupListAsync(CancellationToken ct = default)
        => await QueryCustomAsync<SysUser>(
            "SELECT `id`, `user_name`, `real_name` FROM `sys_user` WHERE `status` = 1 ORDER BY `id`;",
            null, ct);

    public async Task<IReadOnlyList<NameCount>> GetCountByDeptAsync(int top = 8, CancellationToken ct = default)
        => await QueryCustomAsync<NameCount>(
            "SELECT COALESCE(d.`dept_name`, '未分配') AS `Name`, COUNT(1) AS `Count` " +
            "FROM `sys_user` u LEFT JOIN `sys_dept` d ON u.`dept_id` = d.`id` " +
            "GROUP BY COALESCE(d.`dept_name`, '未分配') " +
            "ORDER BY `Count` DESC, `Name` LIMIT @top;",
            new { top = Math.Clamp(top, 1, 50) }, ct);

    public async Task<IReadOnlyList<NameCount>> GetCountByRoleAsync(int top = 8, CancellationToken ct = default)
        => await QueryCustomAsync<NameCount>(
            "SELECT COALESCE(r.`role_name`, '未分配') AS `Name`, COUNT(1) AS `Count` " +
            "FROM `sys_user` u LEFT JOIN `sys_role` r ON u.`role_id` = r.`id` " +
            "GROUP BY COALESCE(r.`role_name`, '未分配') " +
            "ORDER BY `Count` DESC, `Name` LIMIT @top;",
            new { top = Math.Clamp(top, 1, 50) }, ct);

    /// <summary>
    /// 把筛选条件翻译成 WHERE 片段。所有值都走参数化，
    /// 拼进去的只有固定的 SQL 文本。
    /// </summary>
    private static (string Where, DynamicParameters Parameters) BuildFilter(UserQueryFilter filter)
    {
        var sql = new StringBuilder("1 = 1");
        var parameters = new DynamicParameters();

        if (filter.HasKeyword)
        {
            sql.Append(" AND (u.`user_name` LIKE @keyword")
               .Append(" OR u.`real_name` LIKE @keyword")
               .Append(" OR u.`email` LIKE @keyword")
               .Append(" OR u.`phone` LIKE @keyword)");
            parameters.Add("keyword", filter.LikeKeyword);
        }

        if (filter.Status is not null)
        {
            sql.Append(" AND u.`status` = @status");
            parameters.Add("status", (int)filter.Status.Value);
        }

        if (filter.Gender is not null)
        {
            sql.Append(" AND u.`gender` = @gender");
            parameters.Add("gender", (int)filter.Gender.Value);
        }

        if (filter.DeptId is > 0)
        {
            sql.Append(" AND u.`dept_id` = @deptId");
            parameters.Add("deptId", filter.DeptId.Value);
        }

        if (filter.RoleId is > 0)
        {
            sql.Append(" AND u.`role_id` = @roleId");
            parameters.Add("roleId", filter.RoleId.Value);
        }

        return (sql.ToString(), parameters);
    }
}
