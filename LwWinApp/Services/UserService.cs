using LwWinApp.Core;
using LwWinApp.Data.Repositories;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Utils;

namespace LwWinApp.Services;

/// <summary>
/// 用户业务逻辑。
///
/// Service 层的职责是「校验 + 编排 + 记日志」，三条都不该落到 ViewModel 里：
/// ViewModel 只负责界面状态，校验规则要能被别处（比如将来的 Web API）复用，
/// 而操作日志必须由真正改数据的那一层来写，否则漏记。
/// </summary>
public interface IUserService
{
    Task<PagedResult<SysUser>> QueryPagedAsync(UserQueryFilter filter, CancellationToken ct = default);

    Task<SysUser?> GetAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<SysUser>> GetLookupListAsync(CancellationToken ct = default);

    /// <summary>新增用户。plainPassword 为明文，内部负责哈希。</summary>
    Task<SysUser> CreateAsync(SysUser user, string plainPassword, CancellationToken ct = default);

    /// <summary>更新用户。newPassword 为空表示不改密码。</summary>
    Task UpdateAsync(SysUser user, string? newPassword, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);

    Task SetStatusAsync(long id, EnabledStatus status, CancellationToken ct = default);

    /// <summary>重置密码。</summary>
    Task ResetPasswordAsync(long id, string newPassword, CancellationToken ct = default);

    /// <summary>校验账号密码。仅作演示，框架本身还没有登录页。</summary>
    Task<SysUser?> VerifyPasswordAsync(string userName, string password, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class UserService : IUserService
{
    /// <summary>写进操作日志时用的模块名。</summary>
    private const string Module = "用户管理";

    private readonly IUserRepository _users;
    private readonly ILogRepository _logs;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository users, ILogRepository logs, ILogger<UserService> logger)
    {
        _users = users;
        _logs = logs;
        _logger = logger;
    }

    public Task<PagedResult<SysUser>> QueryPagedAsync(UserQueryFilter filter, CancellationToken ct = default)
        => _users.QueryPagedAsync(filter, ct);

    public Task<SysUser?> GetAsync(long id, CancellationToken ct = default)
        => _users.GetDetailAsync(id, ct);

    public Task<IReadOnlyList<SysUser>> GetLookupListAsync(CancellationToken ct = default)
        => _users.GetLookupListAsync(ct);

    public async Task<SysUser> CreateAsync(SysUser user, string plainPassword, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await ValidateAsync(user, plainPassword, isCreate: true, ct);

        user.Password = PasswordHasher.Hash(plainPassword);
        user.CreateTime = DateTime.Now;
        user.UpdateTime = null;

        var id = await _users.InsertAsync(user, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Info, Module,
            $"新增用户「{user.UserName}」（{user.RealName}）",
            CurrentUserName, null, ct);

        _logger.LogInformation("新增用户 {Id} / {UserName}", id, user.UserName);
        return user;
    }

    public async Task UpdateAsync(SysUser user, string? newPassword, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Id <= 0)
            throw new BusinessException("缺少用户 Id，无法更新。");

        var existing = await _users.GetByIdAsync(user.Id, ct)
            ?? throw BusinessException.NotFound($"用户 {user.UserName}");

        await ValidateAsync(user, newPassword, isCreate: false, ct);

        // 密码为空表示不改动，把库里的原值带上，避免整行更新时被清空。
        user.Password = string.IsNullOrWhiteSpace(newPassword)
            ? existing.Password
            : PasswordHasher.Hash(newPassword);

        user.CreateTime = existing.CreateTime;   // 创建时间不可改，用库里的值覆盖
        user.UpdateTime = DateTime.Now;

        await _users.UpdateAsync(user, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Info, Module,
            $"修改用户「{user.UserName}」{DescribeProfileChange(existing, user)}",
            CurrentUserName, null, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var existing = await _users.GetByIdAsync(id, ct)
            ?? throw BusinessException.NotFound("该用户");

        await _users.DeleteAsync(id, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Warn, Module,
            $"删除用户「{existing.UserName}」（{existing.RealName}）",
            CurrentUserName, null, ct);

        _logger.LogInformation("删除用户 {Id} / {UserName}", id, existing.UserName);
    }

    public async Task SetStatusAsync(long id, EnabledStatus status, CancellationToken ct = default)
    {
        var existing = await _users.GetByIdAsync(id, ct)
            ?? throw BusinessException.NotFound("该用户");

        if (existing.Status == status) return;

        existing.Status = status;
        existing.UpdateTime = DateTime.Now;

        await _users.UpdateAsync(existing, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Info, Module,
            $"{(status == EnabledStatus.Enabled ? "启用" : "停用")}用户「{existing.UserName}」",
            CurrentUserName, null, ct);
    }

    public async Task ResetPasswordAsync(long id, string newPassword, CancellationToken ct = default)
    {
        if (!RegexHelper.IsStrongPassword(newPassword))
            throw new BusinessException("密码至少 6 位，且需同时包含字母和数字。")
            {
                FieldName = "密码"
            };

        var existing = await _users.GetByIdAsync(id, ct)
            ?? throw BusinessException.NotFound("该用户");

        existing.Password = PasswordHasher.Hash(newPassword);
        existing.UpdateTime = DateTime.Now;

        await _users.UpdateAsync(existing, null, ct);

        await _logs.WriteAsync(
            LogLevelKind.Warn, Module,
            $"重置用户「{existing.UserName}」的密码",
            CurrentUserName, null, ct);
    }

    public async Task<SysUser?> VerifyPasswordAsync(string userName, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            return null;

        // 列名必须带上 u. 前缀：UserRepository 的表别名是 u，而 sys_dept / sys_role
        // 里也有 status 这类同名列，不限定就会被 MySQL 判为歧义列。
        var user = await _users.QueryFirstAsync("u.`user_name` = @userName", new { userName }, ct: ct);

        if (user is null || !PasswordHasher.Verify(password, user.Password))
        {
            await _logs.WriteAsync(
                LogLevelKind.Warn, Module, $"账号「{userName}」登录失败",
                userName, null, ct);

            return null;
        }

        if (user.Status != EnabledStatus.Enabled)
            throw new BusinessException("该账号已被停用，请联系管理员。");

        // 旧格式（SHA256）验证通过后顺手升级成 PBKDF2，用一次就换掉，无需用户感知。
        if (PasswordHasher.NeedsUpgrade(user.Password))
        {
            user.Password = PasswordHasher.Hash(password);
            user.UpdateTime = DateTime.Now;
            await _users.UpdateAsync(user, null, ct);

            _logger.LogInformation("用户 {UserName} 的密码哈希已升级为 PBKDF2", user.UserName);
        }

        return user;
    }

    // ---------------------------------------------------------------

    /// <summary>
    /// 校验。create 与 update 的差别只在密码：新增必须给，修改可以不给。
    /// </summary>
    private async Task ValidateAsync(SysUser user, string? password, bool isCreate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.UserName))
            throw BusinessException.Required("登录账号");

        if (!RegexHelper.IsUserName(user.UserName))
            throw new BusinessException("账号需以字母开头，由字母、数字、下划线组成，长度 3~20 位。")
            {
                FieldName = "登录账号"
            };

        if (string.IsNullOrWhiteSpace(user.RealName))
            throw BusinessException.Required("姓名");

        if (user.RealName.Trim().Length > 30)
            throw new BusinessException("姓名不能超过 30 个字符。") { FieldName = "姓名" };

        // 邮箱和手机号是选填的，但一旦填了就必须合法——留空和填错是两回事。
        if (!string.IsNullOrWhiteSpace(user.Email) && !RegexHelper.IsEmail(user.Email))
            throw new BusinessException("邮箱格式不正确。") { FieldName = "邮箱" };

        if (!string.IsNullOrWhiteSpace(user.Phone) && !RegexHelper.IsPhone(user.Phone))
            throw new BusinessException("手机号格式不正确。") { FieldName = "手机号" };

        if (!string.IsNullOrWhiteSpace(user.Remark) && user.Remark.Length > 200)
            throw new BusinessException("备注不能超过 200 个字符。") { FieldName = "备注" };

        if (isCreate)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw BusinessException.Required("密码");

            if (!RegexHelper.IsStrongPassword(password))
                throw new BusinessException("密码至少 6 位，且需同时包含字母和数字。") { FieldName = "密码" };
        }
        else if (!string.IsNullOrWhiteSpace(password) && !RegexHelper.IsStrongPassword(password))
        {
            throw new BusinessException("密码至少 6 位，且需同时包含字母和数字。") { FieldName = "密码" };
        }

        // 唯一性检查放在最后：前面都是本地能判定的，这一步要查库。
        // 类型要显式写成 long?，否则三元里的 null 推断不出类型。
        long? excludeId = isCreate ? null : user.Id;

        if (await _users.IsUserNameTakenAsync(user.UserName.Trim(), excludeId, ct))
            throw new BusinessException($"账号「{user.UserName}」已被占用，请换一个。") { FieldName = "登录账号" };
    }

    /// <summary>把「改了哪些字段」写进日志。只提示「已修改」对排查问题没有帮助。</summary>
    private static string DescribeProfileChange(SysUser before, SysUser after)
    {
        var changes = new List<string>();

        if (before.RealName != after.RealName) changes.Add($"姓名 {before.RealName} → {after.RealName}");
        if (before.Email != after.Email) changes.Add($"邮箱 {before.Email.OrDash()} → {after.Email.OrDash()}");
        if (before.Phone != after.Phone) changes.Add($"手机 {before.Phone.OrDash()} → {after.Phone.OrDash()}");
        if (before.Gender != after.Gender) changes.Add($"性别 {before.GenderText} → {after.GenderText}");
        if (before.DeptId != after.DeptId) changes.Add("部门变更");
        if (before.RoleId != after.RoleId) changes.Add("角色变更");
        if (before.Status != after.Status) changes.Add($"状态 {before.StatusText} → {after.StatusText}");

        return changes.Count == 0 ? "（无字段变化）" : "：" + string.Join("，", changes);
    }

    /// <summary>
    /// 操作人。框架里还没有登录态，先用 Windows 账号顶着；
    /// 接入登录后把这里换成当前登录用户即可。
    /// </summary>
    private static string CurrentUserName => Environment.UserName;
}

/// <summary>日志里展示空值的小工具。</summary>
internal static class ServiceStringExtensions
{
    public static string OrDash(this string? value)
        => string.IsNullOrWhiteSpace(value) ? "（空）" : value;
}
