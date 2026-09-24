using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Services;
using LwWinApp.Utils;
using LwWinApp.Views.Dialogs;
using Microsoft.Extensions.Options;

namespace LwWinApp.ViewModels;

/// <summary>
/// 用户管理。
///
/// 这个 ViewModel 是列表页的样板：筛选 → 分页 → 增删改 → 导出。
/// 角色管理、操作日志两个页面基本照它的结构写，差别只在筛选字段和有没有写操作。
/// </summary>
public sealed partial class UserManageViewModel : ViewModelBase
{
    private readonly IUserService _users;
    private readonly IDeptService _depts;
    private readonly IRoleService _roles;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notify;
    private readonly AppOptions _options;

    public UserManageViewModel(
        IUserService users,
        IDeptService depts,
        IRoleService roles,
        IDialogService dialogs,
        INotificationService notify,
        IOptions<AppOptions> options)
    {
        _users = users;
        _depts = depts;
        _roles = roles;
        _dialogs = dialogs;
        _notify = notify;
        _options = options.Value;

        Title = "用户管理";
        _pageSize = _options.PageSize;

        StatusFilterOptions =
        [
            SelectOption.All("全部状态"),
            new SelectOption("启用", EnabledStatus.Enabled),
            new SelectOption("停用", EnabledStatus.Disabled)
        ];

        GenderFilterOptions =
        [
            SelectOption.All("全部性别"),
            new SelectOption("男", Gender.Male),
            new SelectOption("女", Gender.Female),
            new SelectOption("未知", Gender.Unknown)
        ];

        _selectedStatus = StatusFilterOptions[0];
        _selectedGender = GenderFilterOptions[0];
    }

    // ---- 筛选条件 ----

    [ObservableProperty]
    private string? _keyword;

    [ObservableProperty]
    private SelectOption? _selectedStatus;

    [ObservableProperty]
    private SelectOption? _selectedGender;

    [ObservableProperty]
    private SelectOption? _selectedDept;

    [ObservableProperty]
    private SelectOption? _selectedRole;

    // ---- 数据 ----

    [ObservableProperty]
    private IReadOnlyList<SysUser> _items = [];

    [ObservableProperty]
    private SysUser? _selectedUser;

    [ObservableProperty]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _pageSize;

    [ObservableProperty]
    private long _total;

    /// <summary>列表里一条都没有时页面显示的提示语，区分「没数据」和「筛掉了」。</summary>
    public string EmptyHint => HasFilter
        ? "没有符合当前条件的用户，换个条件试试。"
        : "还没有任何用户，点右上角「新增用户」开始吧。";

    /// <summary>「重置」按钮是否可用。</summary>
    public bool HasFilter
        => !string.IsNullOrWhiteSpace(Keyword)
        || SelectedStatus?.Value is not null
        || SelectedGender?.Value is not null
        || SelectedDept?.Value is not null
        || SelectedRole?.Value is not null;

    public IReadOnlyList<int> PageSizeOptions => _options.GetValidPageSizeOptions();

    public IReadOnlyList<SelectOption> StatusFilterOptions { get; }

    public IReadOnlyList<SelectOption> GenderFilterOptions { get; }

    [ObservableProperty]
    private IReadOnlyList<SelectOption> _deptFilterOptions = [SelectOption.All("全部部门")];

    /// <summary>编辑对话框里用的部门选项，第一项是「不设部门」而不是「全部」。</summary>
    public IReadOnlyList<SelectOption> DeptFormOptions { get; private set; } = [new SelectOption("（不设部门）")];

    [ObservableProperty]
    private IReadOnlyList<SelectOption> _roleFilterOptions = [SelectOption.All("全部角色")];

    public IReadOnlyList<SelectOption> RoleFormOptions { get; private set; } = [new SelectOption("（不设角色）")];

    // ---------------------------------------------------------------

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        if (!IsInitialized)
        {
            await RunBusyAsync(async () =>
            {
                await LoadFilterOptionsAsync();
                IsInitialized = true;
            }, "加载筛选项失败");
        }

        await LoadAsync();
    }

    partial void OnPageIndexChanged(int value) => _ = LoadAsync();

    partial void OnPageSizeChanged(int value)
    {
        // 每页条数变化时先回第一页：原来在第 5 页，改成每页 100 条之后
        // 那一页很可能已经不存在了。
        if (PageIndex != 1)
            PageIndex = 1;      // 触发 OnPageIndexChanged 去加载
        else
            _ = LoadAsync();
    }

    partial void OnItemsChanged(IReadOnlyList<SysUser> value) => OnPropertyChanged(nameof(EmptyHint));

    partial void OnKeywordChanged(string? value) => RaiseFilterChanged();

    partial void OnSelectedStatusChanged(SelectOption? value) => RaiseFilterChanged();

    partial void OnSelectedGenderChanged(SelectOption? value) => RaiseFilterChanged();

    partial void OnSelectedDeptChanged(SelectOption? value) => RaiseFilterChanged();

    partial void OnSelectedRoleChanged(SelectOption? value) => RaiseFilterChanged();

    private void RaiseFilterChanged()
    {
        // HasFilter 是从这些字段算出来的，字段变了得手动通知一次。
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(EmptyHint));
    }

    // ---- 命令 ----

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (PageIndex != 1)
        {
            PageIndex = 1;      // 触发 OnPageIndexChanged 去加载
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task ResetFilterAsync()
    {
        // 筛选字段本身不触发查询（只有 PageIndex 会），所以直接赋值即可，
        // 最后统一走一次加载。
        Keyword = null;
        SelectedStatus = StatusFilterOptions[0];
        SelectedGender = GenderFilterOptions[0];
        SelectedDept = DeptFilterOptions.Count > 0 ? DeptFilterOptions[0] : null;
        SelectedRole = RoleFilterOptions.Count > 0 ? RoleFilterOptions[0] : null;

        if (PageIndex != 1)
        {
            PageIndex = 1;      // 触发 OnPageIndexChanged 去加载
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = new UserEditDialog(_users, null, DeptFormOptions, RoleFormOptions);

        await _dialogs.ShowCustomAsync(dialog);

        if (!dialog.Saved) return;

        _notify.Success("用户已创建。");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync(SysUser? user)
    {
        if (user is null) return;

        var dialog = new UserEditDialog(_users, user, DeptFormOptions, RoleFormOptions);

        await _dialogs.ShowCustomAsync(dialog);

        if (!dialog.Saved) return;

        _notify.Success($"用户「{user.UserName}」已更新。");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(SysUser? user)
    {
        if (user is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            $"确定要删除用户「{user.RealName}（{user.UserName}）」吗？\n\n删除后无法恢复。",
            "删除用户",
            primaryText: "删除",
            danger: true);

        if (!confirmed) return;

        var lastOneOnPage = Items.Count <= 1;

        var deleted = await TryRunBusyAsync(
            () => _users.DeleteAsync(user.Id),
            "删除用户失败");

        if (!deleted) return;

        _notify.Success($"用户「{user.UserName}」已删除。");

        // 删的是本页最后一条时后退一页，否则会停在一个空页面上。
        if (lastOneOnPage && PageIndex > 1)
            PageIndex--;        // 触发 OnPageIndexChanged 去加载
        else
            await LoadAsync();
    }

    [RelayCommand]
    private async Task ToggleStatusAsync(SysUser? user)
    {
        if (user is null) return;

        var target = user.Status == EnabledStatus.Enabled
            ? EnabledStatus.Disabled
            : EnabledStatus.Enabled;

        var changed = await TryRunBusyAsync(
            () => _users.SetStatusAsync(user.Id, target),
            "修改状态失败");

        if (!changed) return;

        _notify.Success($"已{(target == EnabledStatus.Enabled ? "启用" : "停用")}用户「{user.UserName}」。");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(SysUser? user)
    {
        if (user is null) return;

        var password = await _dialogs.PromptAsync(
            $"为用户「{user.UserName}」设置新密码。",
            "重置密码",
            placeholder: "至少 6 位，含字母和数字",
            validate: value => RegexHelper.IsStrongPassword(value)
                ? null
                : "密码至少 6 位，且需同时包含字母和数字。");

        if (string.IsNullOrEmpty(password)) return;

        var done = await TryRunBusyAsync(
            () => _users.ResetPasswordAsync(user.Id, password),
            "重置密码失败");

        if (done)
            _notify.Success($"用户「{user.UserName}」的密码已重置。");
    }

    /// <summary>导出当前筛选条件下的全部数据（不只是当前页）。</summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        // 只导出本页意义不大，用户要的是完整结果集；
        // 所以把每页条数临时放到上限再查一次，不动当前分页状态。
        var exported = await RunBusyAsync(async () =>
        {
            var filter = BuildFilter();
            filter.PageIndex = 1;
            filter.PageSize = QueryFilter.MaxPageSize;

            var result = await _users.QueryPagedAsync(filter);

            var path = await CsvExporter.ExportAsync(
                result.Items,
                [
                    ("ID", u => u.Id),
                    ("登录账号", u => u.UserName),
                    ("姓名", u => u.RealName),
                    ("性别", u => u.GenderText),
                    ("部门", u => u.DeptName),
                    ("角色", u => u.RoleName),
                    ("邮箱", u => u.Email),
                    ("手机号", u => u.Phone),
                    ("状态", u => u.StatusText),
                    ("创建时间", u => u.CreateTime),
                    ("备注", u => u.Remark)
                ],
                $"用户列表_{DateTimeHelper.ForFileName()}");

            return (Count: result.Items.Count, Path: path);
        }, "导出失败");

        if (!exported.HasValue) return;

        if (exported.Value.Path is null)
        {
            _notify.Warning("导出失败：无法写入导出目录，请检查设置页里的导出路径。");
            return;
        }

        _notify.Success($"已导出 {exported.Value.Count} 条记录到 {exported.Value.Path}");

        // 顺手把文件选中，省得用户自己去翻目录。
        FileHelper.RevealInExplorer(exported.Value.Path);
    }

    // ---------------------------------------------------------------

    private async Task LoadAsync() => await RunBusyAsync(async () =>
    {
        var filter = BuildFilter();
        var result = await _users.QueryPagedAsync(filter);

        Items = result.Items;
        Total = result.Total;
    }, "加载用户列表失败");

    private UserQueryFilter BuildFilter() => new()
    {
        PageIndex = PageIndex,
        PageSize = PageSize,
        Keyword = Keyword,
        Status = SelectedStatus.AsEnum<EnabledStatus>(),
        Gender = SelectedGender.AsEnum<Gender>(),
        DeptId = SelectedDept.AsLong(),
        RoleId = SelectedRole.AsLong()
    };

    /// <summary>加载部门与角色的下拉项。一次加载，之后靠「刷新」按钮同步。</summary>
    private async Task LoadFilterOptionsAsync()
    {
        var deptOptions = await _depts.GetOptionsAsync();
        var roleOptions = await _roles.GetEnabledListAsync();

        DeptFilterOptions = [SelectOption.All("全部部门"), .. deptOptions.Select(o => new SelectOption(o.DisplayName, o.Id))];
        DeptFormOptions = [new SelectOption("（不设部门）"), .. deptOptions.Select(o => new SelectOption(o.DisplayName, o.Id))];

        RoleFilterOptions = [SelectOption.All("全部角色"), .. roleOptions.Select(r => new SelectOption(r.RoleName, r.Id))];
        RoleFormOptions = [new SelectOption("（不设角色）"), .. roleOptions.Select(r => new SelectOption(r.RoleName, r.Id))];

        // 选中的那个对象必须来自新列表，否则下拉框找不到匹配项会显示成空白。
        SelectedDept = DeptFilterOptions[0];
        SelectedRole = RoleFilterOptions[0];
    }
}
