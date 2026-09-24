using LwWinApp.Core;
using LwWinApp.Data.Repositories;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;

namespace LwWinApp.Services;

/// <summary>角色业务逻辑。相比用户管理简单一档，正好用来展示通用仓储的复用。</summary>
public interface IRoleService
{
    Task<PagedResult<SysRole>> QueryPagedAsync(RoleQueryFilter filter, CancellationToken ct = default);

    Task<SysRole?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>取启用的角色，供下拉框绑定。</summary>
    Task<IReadOnlyList<SysRole>> GetEnabledListAsync(CancellationToken ct = default);

    Task<SysRole> CreateAsync(SysRole role, CancellationToken ct = default);

    Task UpdateAsync(SysRole role, CancellationToken ct = default);

    /// <summary>删除角色。角色下还有用户时会被拒绝。</summary>
    Task DeleteAsync(long id, CancellationToken ct = default);

    Task SetStatusAsync(long id, EnabledStatus status, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class RoleService : IRoleService
{
    private const string Module = "角色管理";

    private readonly IRoleRepository _roles;
    private readonly ILogRepository _logs;
    private readonly ILogger<RoleService> _logger;

    public RoleService(IRoleRepository roles, ILogRepository logs, ILogger<RoleService> logger)
    {
        _roles = roles;
        _logs = logs;
        _logger = logger;
    }

    public Task<PagedResult<SysRole>> QueryPagedAsync(RoleQueryFilter filter, CancellationToken ct = default)
        => _roles.QueryPagedAsync(filter, ct);

    public Task<SysRole?> GetAsync(long id, CancellationToken ct = default)
        => _roles.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<SysRole>> GetEnabledListAsync(CancellationToken ct = default)
        => _roles.GetEnabledListAsync(ct);

    public async Task<SysRole> CreateAsync(SysRole role, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await ValidateAsync(role, isCreate: true, ct);

        role.CreateTime = DateTime.Now;

        await _roles.InsertAsync(role, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Info, Module,
            $"新增角色「{role.RoleName}」（{role.RoleCode}）",
            CurrentUserName, null, ct);

        return role;
    }

    public async Task UpdateAsync(SysRole role, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (role.Id <= 0)
            throw new BusinessException("缺少角色 Id，无法更新。");

        var existing = await _roles.GetByIdAsync(role.Id, ct)
            ?? throw BusinessException.NotFound($"角色 {role.RoleName}");

        await ValidateAsync(role, isCreate: false, ct);

        // 创建时间和关联用户数是展示用的，不能跟着表单一起写回库。
        role.CreateTime = existing.CreateTime;
        role.UserCount = existing.UserCount;

        await _roles.UpdateAsync(role, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Info, Module,
            $"修改角色「{role.RoleName}」",
            CurrentUserName, null, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var existing = await _roles.GetByIdAsync(id, ct)
            ?? throw BusinessException.NotFound("该角色");

        // 外键约束不存在（表是 InnoDB 但没建 FK），所以必须在应用层挡住，
        // 否则删掉角色会留下一批 role_id 指向不存在记录的用户。
        if (await _roles.HasUsersAsync(id, ct))
            throw new BusinessException($"「{existing.RoleName}」下还有用户，请先调整这些用户的角色。");

        await _roles.DeleteAsync(id, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Warn, Module,
            $"删除角色「{existing.RoleName}」（{existing.RoleCode}）",
            CurrentUserName, null, ct);

        _logger.LogInformation("删除角色 {Id} / {RoleName}", id, existing.RoleName);
    }

    public async Task SetStatusAsync(long id, EnabledStatus status, CancellationToken ct = default)
    {
        var existing = await _roles.GetByIdAsync(id, ct)
            ?? throw BusinessException.NotFound("该角色");

        if (existing.Status == status) return;

        existing.Status = status;

        await _roles.UpdateAsync(existing, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Info, Module,
            $"{(status == EnabledStatus.Enabled ? "启用" : "停用")}角色「{existing.RoleName}」",
            CurrentUserName, null, ct);
    }

    // ---------------------------------------------------------------

    private async Task ValidateAsync(SysRole role, bool isCreate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(role.RoleCode))
            throw BusinessException.Required("角色编码");

        // 编码会出现在权限判断里，限制成纯标识符，避免出现空格、中文等难处理的值。
        if (!System.Text.RegularExpressions.Regex.IsMatch(role.RoleCode.Trim(), @"^[a-z][a-z0-9_]{1,29}$"))
        {
            throw new BusinessException("角色编码需为小写字母开头，由小写字母、数字、下划线组成，长度 2~30 位。")
            {
                FieldName = "角色编码"
            };
        }

        if (string.IsNullOrWhiteSpace(role.RoleName))
            throw BusinessException.Required("角色名称");

        if (role.RoleName.Trim().Length > 30)
            throw new BusinessException("角色名称不能超过 30 个字符。") { FieldName = "角色名称" };

        if (!string.IsNullOrWhiteSpace(role.Remark) && role.Remark.Length > 200)
            throw new BusinessException("备注不能超过 200 个字符。") { FieldName = "备注" };

        if (role.Sort < 0)
            throw new BusinessException("排序号不能为负数。") { FieldName = "排序" };

        // 显式写成 long?，否则三元里的 null 推断不出类型。
        long? excludeId = isCreate ? null : role.Id;

        if (await _roles.IsRoleCodeTakenAsync(role.RoleCode.Trim(), excludeId, ct))
            throw new BusinessException($"角色编码「{role.RoleCode}」已存在。") { FieldName = "角色编码" };
    }

    private static string CurrentUserName => Environment.UserName;
}
