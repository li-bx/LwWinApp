using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Services;
using LwWinApp.Views.Dialogs;

namespace LwWinApp.ViewModels;

/// <summary>
/// 部门架构。
///
/// 树由 Service 一次性拼好（<see cref="DeptNode"/> 是不可变的），这里只负责
/// 展示、选中和增删改。任何一次写操作之后都整棵重新拉取 ——
/// 部门数据量很小，局部改树带来的不一致风险远大于省下的那点查询。
/// </summary>
public sealed partial class DeptTreeViewModel : ViewModelBase
{
    private readonly IDeptService _depts;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notify;

    /// <summary>Id → 名称。用来显示「上级部门」的名字，不然得再查一次库。</summary>
    private Dictionary<long, string> _nameById = [];

    public DeptTreeViewModel(IDeptService depts, IDialogService dialogs, INotificationService notify)
    {
        _depts = depts;
        _dialogs = dialogs;
        _notify = notify;

        Title = "部门架构";
    }

    /// <summary>树的数据源。绑到 TreeView 的 ItemsSource 上。</summary>
    [ObservableProperty]
    private IReadOnlyList<DeptNode> _rootNodes = [];

    /// <summary>当前选中的节点。双向同步用，绑到 TreeView.SelectedItem。</summary>
    [ObservableProperty]
    private DeptNode? _selectedNode;

    [ObservableProperty]
    private long _deptCount;

    // ---- 右侧详情面板 ----

    public bool HasSelection => SelectedNode is not null;

    public string SelectedDeptName => SelectedNode?.DeptName ?? "未选择";

    public string SelectedLeader => SelectedNode?.Leader is { Length: > 0 } leader ? leader : "未指定";

    public string SelectedParentName
        => SelectedNode is null
            ? "—"
            : SelectedNode.Dept.ParentId == 0
                ? "（顶级部门）"
                : _nameById.GetValueOrDefault(SelectedNode.Dept.ParentId, "（已删除）");

    public string SelectedStatusText => SelectedNode?.Dept.StatusText ?? "—";

    public bool SelectedIsEnabled => SelectedNode?.Dept.IsEnabled ?? false;

    public string SelectedCreateTime
        => SelectedNode is null ? "—" : SelectedNode.Dept.CreateTime.ToString("yyyy-MM-dd HH:mm");

    public string SelectedChildSummary
        => SelectedNode is null
            ? "—"
            : $"{SelectedNode.Children.Count} 个直接下级";

    public override Task OnNavigatedToAsync(object? parameter) => LoadAsync();

    partial void OnSelectedNodeChanged(DeptNode? value)
    {
        // 详情面板上的每个字段都是从选中节点算出来的，节点一换全部要通知。
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedDeptName));
        OnPropertyChanged(nameof(SelectedLeader));
        OnPropertyChanged(nameof(SelectedParentName));
        OnPropertyChanged(nameof(SelectedStatusText));
        OnPropertyChanged(nameof(SelectedIsEnabled));
        OnPropertyChanged(nameof(SelectedCreateTime));
        OnPropertyChanged(nameof(SelectedChildSummary));
    }

    /// <summary>由页面在 TreeView.ItemInvoked 里调用。</summary>
    public void SelectNode(DeptNode? node) => SelectedNode = node;

    // ---- 命令 ----

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task AddRootAsync()
    {
        await ShowEditDialogAsync(null, parentId: 0);
    }

    [RelayCommand]
    private async Task AddChildAsync()
    {
        // 没选中任何节点时挂到根上，比什么都不做要好——用户点的是「新增下级」，
        // 他想要的显然是一个新部门。
        var parentId = SelectedNode?.Id ?? 0;

        await ShowEditDialogAsync(null, parentId);
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedNode is null)
        {
            await _dialogs.ShowInfoAsync("请先在左侧选择一个部门。", "未选择部门");
            return;
        }

        await ShowEditDialogAsync(SelectedNode.Dept, parentId: 0);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedNode is null)
        {
            await _dialogs.ShowInfoAsync("请先在左侧选择一个部门。", "未选择部门");
            return;
        }

        var node = SelectedNode;

        var confirmed = await _dialogs.ConfirmAsync(
            $"确定要删除部门「{node.DeptName}」吗？\n\n删除后无法恢复。",
            "删除部门",
            primaryText: "删除",
            danger: true);

        if (!confirmed) return;

        var deleted = await TryRunBusyAsync(
            () => _depts.DeleteAsync(node.Id),
            "删除部门失败");

        if (!deleted) return;

        _notify.Success($"部门「{node.DeptName}」已删除。");

        // 删掉的正是当前选中项，清空选择避免详情面板停留在一个已不存在的部门上。
        SelectedNode = null;
        await LoadAsync();
    }

    // ---------------------------------------------------------------

    private async Task ShowEditDialogAsync(SysDept? existing, long parentId)
    {
        // 编辑时不能把自己或自己的子孙选成上级，否则树会成环。
        var excludeId = existing?.Id;
        var parentOptions = await BuildParentOptionsAsync(excludeId);

        var dialog = new DeptEditDialog(_depts, existing, parentId, parentOptions);

        await _dialogs.ShowCustomAsync(dialog);

        if (!dialog.Saved) return;

        _notify.Success(existing is null ? "部门已创建。" : $"部门「{existing.DeptName}」已更新。");
        await LoadAsync();
    }

    private async Task LoadAsync() => await RunBusyAsync(async () =>
    {
        var selectedId = SelectedNode?.Id;

        var tree = await _depts.GetTreeAsync();

        _nameById = new Dictionary<long, string>();
        IndexNames(tree, _nameById);

        RootNodes = tree;
        DeptCount = await _depts.CountAsync();

        // 重新拉取后节点对象都是新的，按 Id 把选中项接回去，
        // 否则每次改完数据右侧详情面板都会空掉。
        SelectedNode = selectedId is null ? null : Find(tree, selectedId.Value);
    }, "加载部门树失败");

    private static void IndexNames(IReadOnlyList<DeptNode> nodes, Dictionary<long, string> target)
    {
        foreach (var node in nodes)
        {
            target[node.Id] = node.DeptName;
            IndexNames(node.Children, target);
        }
    }

    private static DeptNode? Find(IReadOnlyList<DeptNode> nodes, long id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id) return node;

            if (Find(node.Children, id) is { } found) return found;
        }

        return null;
    }

    /// <summary>
    /// 生成上级部门下拉项。excludeId 及其所有子孙都会被排除掉——
    /// 让用户能选中它们，等于给自己挖了个成环的坑。
    /// </summary>
    private async Task<IReadOnlyList<SelectOption>> BuildParentOptionsAsync(long? excludeId)
    {
        var tree = await _depts.GetTreeAsync();
        var options = new List<SelectOption> { new("（顶级部门）", 0L) };

        Walk(tree);

        return options;

        void Walk(IReadOnlyList<DeptNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Id == excludeId) continue;

                options.Add(new SelectOption(node.IndentedName, node.Id));
                Walk(node.Children);
            }
        }
    }
}
