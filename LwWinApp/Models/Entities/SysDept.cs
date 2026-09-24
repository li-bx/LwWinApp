using LwWinApp.Core;
using LwWinApp.Data;

namespace LwWinApp.Models.Entities;

/// <summary>部门表，自引用层级结构（parent_id = 0 表示顶层）。</summary>
[Table("sys_dept")]
public sealed class SysDept
{
    [Key]
    public long Id { get; set; }

    /// <summary>上级部门 Id，0 表示顶级。</summary>
    public long ParentId { get; set; }

    public string DeptName { get; set; } = string.Empty;

    public int Sort { get; set; }

    public string? Leader { get; set; }

    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;

    public DateTime CreateTime { get; set; } = DateTime.Now;

    [Ignore]
    public string StatusText => Status == EnabledStatus.Enabled ? "启用" : "停用";

    [Ignore]
    public bool IsEnabled => Status == EnabledStatus.Enabled;
}
