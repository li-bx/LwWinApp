using LwWinApp.Core;
using LwWinApp.Data;

namespace LwWinApp.Models.Entities;

/// <summary>角色表。</summary>
[Table("sys_role")]
public sealed class SysRole
{
    [Key]
    public long Id { get; set; }

    /// <summary>角色编码，唯一，如 admin / operator。</summary>
    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public int Sort { get; set; }

    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;

    public string? Remark { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>关联的用户数，由聚合查询填充。</summary>
    [Ignore]
    public int UserCount { get; set; }

    [Ignore]
    public string StatusText => Status == EnabledStatus.Enabled ? "启用" : "停用";

    [Ignore]
    public bool IsEnabled => Status == EnabledStatus.Enabled;

    /// <summary>供「停用」徽标用。x:Bind 能把 bool 直接转成 Visibility。</summary>
    [Ignore]
    public bool IsDisabled => !IsEnabled;

    // 下面几个是给表格直接显示的字符串版本。
    // x:Bind 对「非字符串 → TextBlock.Text」不做隐式转换，所以在这里转好，
    // 而不是在每个单元格上挂转换器。
    [Ignore]
    public string SortText => Sort.ToString();

    [Ignore]
    public string UserCountText => UserCount.ToString();

    [Ignore]
    public string CreateTimeText => CreateTime.ToString("yyyy-MM-dd HH:mm");

    [Ignore]
    public string RemarkDisplay => string.IsNullOrWhiteSpace(Remark) ? "—" : Remark;
}
