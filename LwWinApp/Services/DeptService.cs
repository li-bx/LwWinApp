using LwWinApp.Core;
using LwWinApp.Data.Repositories;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;

namespace LwWinApp.Services;

/// <summary>部门业务逻辑。核心是层级树的拼装与「不能形成环」的校验。</summary>
public interface IDeptService
{
    /// <summary>取整棵部门树。</summary>
    Task<IReadOnlyList<DeptNode>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>取带缩进的扁平列表，供父级/所属部门下拉框绑定。</summary>
    Task<IReadOnlyList<DeptOption>> GetOptionsAsync(bool includeNoneOption = false, CancellationToken ct = default);

    Task<SysDept?> GetAsync(long id, CancellationToken ct = default);

    Task<SysDept> CreateAsync(SysDept dept, CancellationToken ct = default);

    Task UpdateAsync(SysDept dept, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);

    Task<int> GetNextSortAsync(long parentId, CancellationToken ct = default);

    Task<long> CountAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class DeptService : IDeptService
{
    private const string Module = "部门架构";

    /// <summary>防止数据损坏（A 的父是 B，B 的父是 A）导致拼树时无限递归。</summary>
    private const int MaxDepth = 32;

    private readonly IDeptRepository _depts;
    private readonly ILogRepository _logs;
    private readonly ILogger<DeptService> _logger;

    public DeptService(IDeptRepository depts, ILogRepository logs, ILogger<DeptService> logger)
    {
        _depts = depts;
        _logs = logs;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DeptNode>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await _depts.GetTreeSourceAsync(ct);
        return BuildTree(all);
    }

    public async Task<IReadOnlyList<DeptOption>> GetOptionsAsync(
        bool includeNoneOption = false, CancellationToken ct = default)
    {
        var tree = await GetTreeAsync(ct);
        var options = new List<DeptOption>();

        if (includeNoneOption)
            options.Add(DeptOption.None("（不设部门）"));

        Flatten(tree, options);
        return options;
    }

    public Task<SysDept?> GetAsync(long id, CancellationToken ct = default)
        => _depts.GetByIdAsync(id, ct);

    public async Task<SysDept> CreateAsync(SysDept dept, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dept);

        await ValidateAsync(dept, isCreate: true, ct);

        if (dept.Sort <= 0)
            dept.Sort = await _depts.GetMaxSortAsync(dept.ParentId, ct) + 1;

        dept.CreateTime = DateTime.Now;

        await _depts.InsertAsync(dept, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Info, Module,
            $"新增部门「{dept.DeptName}」",
            CurrentUserName, null, ct);

        return dept;
    }

    public async Task UpdateAsync(SysDept dept, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dept);

        if (dept.Id <= 0)
            throw new BusinessException("缺少部门 Id，无法更新。");

        var existing = await _depts.GetByIdAsync(dept.Id, ct)
            ?? throw BusinessException.NotFound($"部门 {dept.DeptName}");

        await ValidateAsync(dept, isCreate: false, ct);

        // 把自己挂到自己的子孙下面会让整棵树断掉（那部分再也遍历不到）。
        if (dept.ParentId != existing.ParentId && dept.ParentId > 0)
        {
            if (dept.ParentId == dept.Id)
                throw new BusinessException("上级部门不能是自己。") { FieldName = "上级部门" };

            if (await IsDescendantAsync(dept.ParentId, dept.Id, ct))
                throw new BusinessException("上级部门不能是自己的下级，这样会形成循环。") { FieldName = "上级部门" };
        }

        dept.CreateTime = existing.CreateTime;

        await _depts.UpdateAsync(dept, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Info, Module,
            $"修改部门「{dept.DeptName}」",
            CurrentUserName, null, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var existing = await _depts.GetByIdAsync(id, ct)
            ?? throw BusinessException.NotFound("该部门");

        if (await _depts.HasChildrenAsync(id, ct))
            throw new BusinessException($"「{existing.DeptName}」下还有子部门，请先删除或移走它们。");

        if (await _depts.HasUsersAsync(id, ct))
            throw new BusinessException($"「{existing.DeptName}」下还有用户，请先调整这些用户的部门。");

        await _depts.DeleteAsync(id, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Warn, Module,
            $"删除部门「{existing.DeptName}」",
            CurrentUserName, null, ct);

        _logger.LogInformation("删除部门 {Id} / {DeptName}", id, existing.DeptName);
    }

    public async Task<int> GetNextSortAsync(long parentId, CancellationToken ct = default)
        => await _depts.GetMaxSortAsync(parentId, ct) + 1;

    public Task<long> CountAsync(CancellationToken ct = default)
        => _depts.CountAsync(null, null, ct);

    // ---------------------------------------------------------------

    /// <summary>
    /// 把平铺列表拼成树。
    ///
    /// 父级不存在的节点（父被删了、parent_id 写错了）会被提到顶层，
    /// 而不是无声地丢掉——数据有问题时应该看得见。
    /// </summary>
    private IReadOnlyList<DeptNode> BuildTree(IReadOnlyList<SysDept> all)
    {
        var byParent = all
            .GroupBy(dept => dept.ParentId)
            .ToDictionary(group => group.Key, group => group.OrderBy(d => d.Sort).ThenBy(d => d.Id).ToList());

        var ids = all.Select(d => d.Id).ToHashSet();

        var roots = all
            .Where(d => d.ParentId == 0 || !ids.Contains(d.ParentId))
            .OrderBy(d => d.Sort)
            .ThenBy(d => d.Id)
            .ToList();

        var orphanCount = all.Count(d => d.ParentId != 0 && !ids.Contains(d.ParentId));
        if (orphanCount > 0)
            _logger.LogWarning("有 {Count} 个部门的上级不存在，已在树中提升到顶层", orphanCount);

        return roots.Select(dept => BuildNode(dept, byParent, 0)).ToList();
    }

    private DeptNode BuildNode(SysDept dept, Dictionary<long, List<SysDept>> byParent, int depth)
    {
        if (depth >= MaxDepth || !byParent.TryGetValue(dept.Id, out var children))
            return new DeptNode(dept, [], Math.Min(depth, MaxDepth));

        var nodes = children
            .Select(child => BuildNode(child, byParent, depth + 1))
            .ToList();

        return new DeptNode(dept, nodes, depth);
    }

    private static void Flatten(IReadOnlyList<DeptNode> nodes, List<DeptOption> target)
    {
        foreach (var node in nodes)
        {
            target.Add(new DeptOption(node.Id, node.DeptName, node.Depth));
            Flatten(node.Children, target);
        }
    }

    /// <summary>candidate 是否是 ancestorId 的子孙。逐级往上走，遇到环就停。</summary>
    private async Task<bool> IsDescendantAsync(long candidateId, long ancestorId, CancellationToken ct)
    {
        // 一次取全表在内存里判断，比每层一次查询更省往返：部门表本来就不大。
        var all = await _depts.GetTreeSourceAsync(ct);
        var parentOf = all.ToDictionary(d => d.Id, d => d.ParentId);

        var current = candidateId;
        var guard = 0;

        while (guard++ < MaxDepth && parentOf.TryGetValue(current, out var parent) && parent != 0)
        {
            if (parent == ancestorId) return true;
            current = parent;
        }

        return false;
    }

    private async Task ValidateAsync(SysDept dept, bool isCreate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dept.DeptName))
            throw BusinessException.Required("部门名称");

        if (dept.DeptName.Trim().Length > 50)
            throw new BusinessException("部门名称不能超过 50 个字符。") { FieldName = "部门名称" };

        if (dept.ParentId < 0)
            throw new BusinessException("上级部门不合法。") { FieldName = "上级部门" };

        if (!string.IsNullOrWhiteSpace(dept.Leader) && dept.Leader.Length > 30)
            throw new BusinessException("负责人不能超过 30 个字符。") { FieldName = "负责人" };

        if (dept.Sort < 0)
            throw new BusinessException("排序号不能为负数。") { FieldName = "排序" };

        // 新增时父级必须存在，否则会凭空多出一棵挂在不存在节点下的子树。
        if (isCreate && dept.ParentId > 0 && !await _depts.ExistsAsync(dept.ParentId, ct))
            throw new BusinessException("选择的上级部门不存在，请刷新后重试。") { FieldName = "上级部门" };

        // 同级下不允许重名，否则树里会出现两个一模一样的节点，用户无从分辨。
        var siblings = await _depts.QueryAsync(
            "`parent_id` = @parentId AND `dept_name` = @name",
            new { parentId = dept.ParentId, name = dept.DeptName.Trim() },
            ct: ct);

        if (siblings.Any(s => s.Id != dept.Id))
            throw new BusinessException($"同级下已存在名为「{dept.DeptName}」的部门。") { FieldName = "部门名称" };
    }

    private static string CurrentUserName => Environment.UserName;
}
