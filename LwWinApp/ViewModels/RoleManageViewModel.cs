using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Services;
using LwWinApp.Utils;
using LwWinApp.Views.Dialogs;
using Microsoft.Extensions.Options;

namespace LwWinApp.ViewModels;

/// <summary>
/// 角色管理。结构照搬用户管理，只是筛选字段少一些 ——
/// 这正好说明通用仓储和 Service 层复用得住。
/// </summary>
public sealed partial class RoleManageViewModel : ViewModelBase
{
    private readonly IRoleService _roles;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notify;
    private readonly AppOptions _options;

    public RoleManageViewModel(
        IRoleService roles,
        IDialogService dialogs,
        INotificationService notify,
        IOptions<AppOptions> options)
    {
        _roles = roles;
        _dialogs = dialogs;
        _notify = notify;
        _options = options.Value;

        Title = "角色管理";
        _pageSize = _options.PageSize;

        StatusFilterOptions =
        [
            SelectOption.All("全部状态"),
            new SelectOption("启用", EnabledStatus.Enabled),
            new SelectOption("停用", EnabledStatus.Disabled)
        ];

        _selectedStatus = StatusFilterOptions[0];
    }

    [ObservableProperty]
    private string? _keyword;

    [ObservableProperty]
    private SelectOption? _selectedStatus;

    [ObservableProperty]
    private IReadOnlyList<SysRole> _items = [];

    [ObservableProperty]
    private SysRole? _selectedRole;

    [ObservableProperty]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _pageSize;

    [ObservableProperty]
    private long _total;

    public IReadOnlyList<SelectOption> StatusFilterOptions { get; }

    public IReadOnlyList<int> PageSizeOptions => _options.GetValidPageSizeOptions();

    public bool HasFilter => !string.IsNullOrWhiteSpace(Keyword) || SelectedStatus?.Value is not null;

    public string EmptyHint => HasFilter
        ? "没有符合当前条件的角色。"
        : "还没有任何角色，点右上角「新增角色」开始吧。";

    public override Task OnNavigatedToAsync(object? parameter)
    {
        IsInitialized = true;
        return LoadAsync();
    }

    partial void OnPageIndexChanged(int value) => _ = LoadAsync();

    partial void OnPageSizeChanged(int value)
    {
        if (PageIndex != 1)
            PageIndex = 1;
        else
            _ = LoadAsync();
    }

    partial void OnItemsChanged(IReadOnlyList<SysRole> value) => OnPropertyChanged(nameof(EmptyHint));

    partial void OnKeywordChanged(string? value) => RaiseFilterChanged();

    partial void OnSelectedStatusChanged(SelectOption? value) => RaiseFilterChanged();

    private void RaiseFilterChanged()
    {
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(EmptyHint));
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (PageIndex != 1)
        {
            PageIndex = 1;
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task ResetFilterAsync()
    {
        Keyword = null;
        SelectedStatus = StatusFilterOptions[0];

        if (PageIndex != 1)
        {
            PageIndex = 1;
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = new RoleEditDialog(_roles, null);
        await _dialogs.ShowCustomAsync(dialog);

        if (!dialog.Saved) return;

        _notify.Success("角色已创建。");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync(SysRole? role)
    {
        if (role is null) return;

        var dialog = new RoleEditDialog(_roles, role);
        await _dialogs.ShowCustomAsync(dialog);

        if (!dialog.Saved) return;

        _notify.Success($"角色「{role.RoleName}」已更新。");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(SysRole? role)
    {
        if (role is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            $"确定要删除角色「{role.RoleName}」吗？\n\n删除后无法恢复。",
            "删除角色",
            primaryText: "删除",
            danger: true);

        if (!confirmed) return;

        var lastOneOnPage = Items.Count <= 1;

        // 角色下还有用户时 Service 会抛业务异常，这里不用自己判断。
        var deleted = await TryRunBusyAsync(
            () => _roles.DeleteAsync(role.Id),
            "删除角色失败");

        if (!deleted) return;

        _notify.Success($"角色「{role.RoleName}」已删除。");

        if (lastOneOnPage && PageIndex > 1)
            PageIndex--;
        else
            await LoadAsync();
    }

    [RelayCommand]
    private async Task ToggleStatusAsync(SysRole? role)
    {
        if (role is null) return;

        var target = role.Status == EnabledStatus.Enabled ? EnabledStatus.Disabled : EnabledStatus.Enabled;

        var changed = await TryRunBusyAsync(
            () => _roles.SetStatusAsync(role.Id, target),
            "修改状态失败");

        if (!changed) return;

        _notify.Success($"已{(target == EnabledStatus.Enabled ? "启用" : "停用")}角色「{role.RoleName}」。");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var exported = await RunBusyAsync(async () =>
        {
            var filter = BuildFilter();
            filter.PageIndex = 1;
            filter.PageSize = QueryFilter.MaxPageSize;

            var result = await _roles.QueryPagedAsync(filter);

            var path = await CsvExporter.ExportAsync(
                result.Items,
                [
                    ("ID", r => r.Id),
                    ("角色编码", r => r.RoleCode),
                    ("角色名称", r => r.RoleName),
                    ("排序", r => r.Sort),
                    ("关联用户数", r => r.UserCount),
                    ("状态", r => r.StatusText),
                    ("创建时间", r => r.CreateTime),
                    ("备注", r => r.Remark)
                ],
                $"角色列表_{DateTimeHelper.ForFileName()}");

            return (Count: result.Items.Count, Path: path);
        }, "导出失败");

        if (!exported.HasValue) return;

        if (exported.Value.Path is null)
        {
            _notify.Warning("导出失败：无法写入导出目录。");
            return;
        }

        _notify.Success($"已导出 {exported.Value.Count} 条记录到 {exported.Value.Path}");
        FileHelper.RevealInExplorer(exported.Value.Path);
    }

    // ---------------------------------------------------------------

    private async Task LoadAsync() => await RunBusyAsync(async () =>
    {
        var result = await _roles.QueryPagedAsync(BuildFilter());

        Items = result.Items;
        Total = result.Total;
    }, "加载角色列表失败");

    private RoleQueryFilter BuildFilter() => new()
    {
        PageIndex = PageIndex,
        PageSize = PageSize,
        Keyword = Keyword,
        Status = SelectedStatus.AsEnum<EnabledStatus>()
    };
}
