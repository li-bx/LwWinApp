using LwWinApp.Core;
using LwWinApp.Data;

namespace LwWinApp.Models.Entities;

/// <summary>用户表。列名按 snake_case 约定自动映射，无需逐个标注。</summary>
[Table("sys_user")]
public sealed class SysUser
{
    [Key]
    public long Id { get; set; }

    /// <summary>登录账号，唯一。</summary>
    public string UserName { get; set; } = string.Empty;

    public string RealName { get; set; } = string.Empty;

    /// <summary>存储的是加盐后的哈希，不存明文。写库前由 UserService 处理。</summary>
    public string Password { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public Gender Gender { get; set; } = Gender.Unknown;

    public long? DeptId { get; set; }

    public long? RoleId { get; set; }

    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;

    public string? Remark { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.Now;

    public DateTime? UpdateTime { get; set; }

    /// <summary>列表 JOIN 出来的部门名，只读展示，不参与写库。</summary>
    [Ignore]
    public string? DeptName { get; set; }

    /// <summary>列表 JOIN 出来的角色名，只读展示，不参与写库。</summary>
    [Ignore]
    public string? RoleName { get; set; }

    [Ignore]
    public string GenderText => Gender switch
    {
        Gender.Male => "男",
        Gender.Female => "女",
        _ => "未知"
    };

    [Ignore]
    public string StatusText => Status == EnabledStatus.Enabled ? "启用" : "停用";

    /// <summary>供 UI 判断状态徽标配色。</summary>
    [Ignore]
    public bool IsEnabled => Status == EnabledStatus.Enabled;

    /// <summary>
    /// 界面上要显示「停用」那一版徽标时用它。
    /// x:Bind 能把 bool 直接转成 Visibility，用它比自己配一个取反转换器省事。
    /// </summary>
    [Ignore]
    public bool IsDisabled => !IsEnabled;

    /// <summary>部门未分配时显示占位文字，避免表格里出现空单元格。</summary>
    [Ignore]
    public string DeptDisplay => string.IsNullOrWhiteSpace(DeptName) ? "（未分配）" : DeptName;

    /// <summary>角色未分配时的占位文字。</summary>
    [Ignore]
    public string RoleDisplay => string.IsNullOrWhiteSpace(RoleName) ? "（未分配）" : RoleName;
}
