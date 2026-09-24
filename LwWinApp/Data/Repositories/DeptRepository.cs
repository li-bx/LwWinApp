using LwWinApp.Core;
using LwWinApp.Models.Entities;

namespace LwWinApp.Data.Repositories;

public interface IDeptRepository : IRepository<SysDept>
{
    /// <summary>取全部部门（按父子顺序排好），供构建树用。</summary>
    Task<IReadOnlyList<SysDept>> GetTreeSourceAsync(CancellationToken ct = default);

    /// <summary>是否还有下级部门，删除前校验用。</summary>
    Task<bool> HasChildrenAsync(long deptId, CancellationToken ct = default);

    /// <summary>部门下是否还有用户，删除前校验用。</summary>
    Task<bool> HasUsersAsync(long deptId, CancellationToken ct = default);

    /// <summary>取某父级下最大的排序号，新增时用来排在最后。</summary>
    Task<int> GetMaxSortAsync(long parentId, CancellationToken ct = default);
}

/// <summary>部门仓储。层级关系由 parent_id 自引用维护。</summary>
public sealed class DeptRepository : RepositoryBase<SysDept>, IDeptRepository
{
    public DeptRepository(IDatabaseFactory db, ILogger<DeptRepository> logger) : base(db, logger) { }

    public async Task<IReadOnlyList<SysDept>> GetTreeSourceAsync(CancellationToken ct = default)
        => await QueryCustomAsync<SysDept>(
            "SELECT * FROM `sys_dept` ORDER BY `parent_id`, `sort`, `id`;",
            null, ct);

    public async Task<bool> HasChildrenAsync(long deptId, CancellationToken ct = default)
        => await ScalarAsync<long>(
            "SELECT COUNT(1) FROM `sys_dept` WHERE `parent_id` = @deptId;",
            new { deptId }, ct: ct) > 0;

    public async Task<bool> HasUsersAsync(long deptId, CancellationToken ct = default)
        => await ScalarAsync<long>(
            "SELECT COUNT(1) FROM `sys_user` WHERE `dept_id` = @deptId;",
            new { deptId }, ct: ct) > 0;

    public async Task<int> GetMaxSortAsync(long parentId, CancellationToken ct = default)
        => await ScalarAsync<int?>(
            "SELECT MAX(`sort`) FROM `sys_dept` WHERE `parent_id` = @parentId;",
            new { parentId }, ct: ct) ?? 0;
}
