using LwWinApp.Models.Entities;

namespace LwWinApp.Models.Dtos;

/// <summary>
/// 部门树节点。
///
/// 刻意做成不可变的：服务层一次性把整棵树拼好，UI 只负责展示。
/// 如果让节点自带 Children 集合并允许 UI 修改，就会出现「树和数据库不一致」
/// 这种最难查的问题——改完之后谁都不知道哪边是对的。
/// </summary>
public sealed class DeptNode
{
    public DeptNode(SysDept dept, IReadOnlyList<DeptNode> children, int depth)
    {
        Dept = dept;
        Children = children;
        Depth = depth;
    }

    public SysDept Dept { get; }

    public IReadOnlyList<DeptNode> Children { get; }

    /// <summary>层级，顶层为 0。</summary>
    public int Depth { get; }

    public long Id => Dept.Id;

    public string DeptName => Dept.DeptName;

    public string? Leader => Dept.Leader;

    public int Sort => Dept.Sort;

    public bool HasChildren => Children.Count > 0;

    /// <summary>节点标题，带层级缩进，用于不便用 TreeView 的地方（如下拉框）。</summary>
    public string IndentedName => new string(' ', Depth * 4) + DeptName;
}

/// <summary>
/// 下拉框用的扁平部门项。TreeView 不好放在表单里，父级选择用带缩进的平铺列表更实际。
/// </summary>
public sealed class DeptOption
{
    public DeptOption(long id, string deptName, int depth)
    {
        Id = id;
        DeptName = deptName;
        Depth = depth;
    }

    public long Id { get; }

    public string DeptName { get; }

    public int Depth { get; }

    /// <summary>带缩进的显示文本，直接绑定到 ComboBox 的 Text 上。</summary>
    public string DisplayName => Depth <= 0 ? DeptName : new string(' ', Depth * 4) + DeptName;

    /// <summary>「（不设部门）」这个特殊项，Id 为 0，父级下拉里代表顶层。</summary>
    public static DeptOption None(string text) => new(0, text, 0);
}
