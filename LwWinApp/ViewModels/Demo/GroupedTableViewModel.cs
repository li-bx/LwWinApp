using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LwWinApp.Controls;
using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Services;
using LwWinApp.Utils;

namespace LwWinApp.ViewModels.Demo;

// ===========================================================================
// 数据文档：和 Assets/Data/GroupedTableSample.json 一一对应。
//
// 属性名必须和 JSON 里的键名完全一致 —— JsonHelper 的命名策略是 null
// （PropertyNamingPolicy = null），也没有开大小写不敏感，所以 JSON 里写
// camelCase 只会得到一堆 null，而且不会报错，界面上就是一片空白。
//
// 保存时按同一套类型往回写，所以存出来的文件可以直接当种子数据用。
// ===========================================================================

/// <summary>整份分组数据。</summary>
public sealed class GroupedTableDocument
{
    public List<GroupedMajorNode> Majors { get; set; } = [];
}

/// <summary>一级分组：大类。</summary>
public sealed class GroupedMajorNode
{
    public string Name { get; set; } = string.Empty;

    public List<GroupedSubNode> Subs { get; set; } = [];
}

/// <summary>二级分组：小类。</summary>
public sealed class GroupedSubNode
{
    public string Name { get; set; } = string.Empty;

    public List<GroupedItemNode> Items { get; set; } = [];
}

/// <summary>一行数据。前三个字段只读，后两个可编辑。</summary>
public sealed class GroupedItemNode
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Spec { get; set; } = string.Empty;

    /// <summary>第 4 列，可编辑（文本框）。</summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>第 5 列，可编辑（下拉框）。</summary>
    public string Status { get; set; } = string.Empty;
}

// ===========================================================================
// 行模型：三种行（大类 / 小类 / 数据）塞进同一个 ListView，靠模板选择器区分。
//
// ListView 原生只支持「一级」分组（CollectionViewSource 的 IsSourceGrouped），
// 两级分组没有现成支持，所以这里自己做一层折叠逻辑：
// 每次展开/折叠后把树「拍平」成一个列表重新绑给 ListView。
// 关键点是**拍平时复用同一批行对象**，不重新 new —— 否则折叠一下就丢编辑。
// ===========================================================================

/// <summary>表格里的一行。</summary>
public interface IGroupedRow
{
    /// <summary>折叠状态。数据行不参与折叠，恒为 true。</summary>
    bool IsExpanded { get; }
}

/// <summary>
/// 折叠箭头用到的字形，取自「Segoe Fluent Icons」。
/// 和 Themes/Icons.xaml 里的 IconChevronDown / IconChevronRight 是同一个字形，
/// 那边是给 XAML 用的字符串资源，C# 这边只能再写一份。
/// </summary>
internal static class GroupGlyph
{
    public const string ChevronDown = "";

    public const string ChevronRight = "";
}

/// <summary>一级分组行：大类。</summary>
public sealed partial class MajorGroupRow : ObservableObject, IGroupedRow
{
    public MajorGroupRow(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>下面的小类。加载时一次性灌满，之后只读。</summary>
    public List<SubGroupRow> Subs { get; } = [];

    [ObservableProperty]
    private bool _isExpanded = true;

    partial void OnIsExpandedChanged(bool value) => RefreshCounters();

    public string ChevronGlyph => IsExpanded ? GroupGlyph.ChevronDown : GroupGlyph.ChevronRight;

    /// <summary>折叠箭头旁边那串统计，让小类/行数/改动数一眼可见。</summary>
    public string Summary
    {
        get
        {
            var rows = Subs.Sum(s => s.Items.Count);
            var edited = Subs.Sum(s => s.Items.Count(i => i.IsDirty));

            return edited > 0
                ? $"{Subs.Count} 个小类 · {rows} 条 · 已改 {edited} 条"
                : $"{Subs.Count} 个小类 · {rows} 条";
        }
    }

    /// <summary>下面的行改动后调用：箭头方向和统计都要跟着变。</summary>
    public void RefreshCounters()
    {
        OnPropertyChanged(nameof(ChevronGlyph));
        OnPropertyChanged(nameof(Summary));
    }
}

/// <summary>二级分组行：小类。</summary>
public sealed partial class SubGroupRow : ObservableObject, IGroupedRow
{
    public SubGroupRow(string name, MajorGroupRow parent)
    {
        Name = name;
        Parent = parent;
    }

    public string Name { get; }

    /// <summary>所属大类。统计数据要一路往上冒泡。</summary>
    public MajorGroupRow Parent { get; }

    public List<GroupedItemRow> Items { get; } = [];

    [ObservableProperty]
    private bool _isExpanded = true;

    partial void OnIsExpandedChanged(bool value) => RefreshCounters();

    public string ChevronGlyph => IsExpanded ? GroupGlyph.ChevronDown : GroupGlyph.ChevronRight;

    public string Summary
    {
        get
        {
            var edited = Items.Count(i => i.IsDirty);

            return edited > 0 ? $"{Items.Count} 条 · 已改 {edited} 条" : $"{Items.Count} 条";
        }
    }

    public void RefreshCounters()
    {
        OnPropertyChanged(nameof(ChevronGlyph));
        OnPropertyChanged(nameof(Summary));
    }
}

/// <summary>
/// 数据行。前 3 列原样展示，第 4 列多行文本框、第 5 列下拉框，改动即时回写到这里。
/// </summary>
public sealed partial class GroupedItemRow : ObservableObject, IGroupedRow
{
    /// <summary>行高下限。ListViewItem 的 MinHeight 是 40，比它矮会和容器打架。</summary>
    public const double MinRowHeight = 44;

    /// <summary>行高上限。纯粹是防止一路往下拖把行拉到没边。</summary>
    public const double MaxRowHeight = 400;

    /// <summary>默认行高。比单行文本高一些，多行输入的备注能露出两三行。</summary>
    private const double DefaultRowHeight = 44;

    private readonly Action<GroupedItemRow>? _onEdited;

    // 「原值」。和当前值不一致就算这行改过；保存成功后由 AcceptChanges 推进过来。
    private string _originalRemark;
    private SelectOption? _originalStatus;

    public GroupedItemRow(
        GroupedItemNode node,
        IReadOnlyList<SelectOption> statusOptions,
        SubGroupRow parent,
        TableLayout layout,
        Action<GroupedItemRow>? onEdited)
    {
        Code = node.Code;
        Name = node.Name;
        Spec = node.Spec;
        Parent = parent;
        StatusOptions = statusOptions;
        Layout = layout;
        _onEdited = onEdited;

        // 直接写字段而不是走属性：构造函数里不该触发「已修改」回调，
        // 而且原值得先落地，不然第一帧就会显示成「改过」。
        _remark = node.Remark;
        _status = statusOptions.FirstOrDefault(o => string.Equals(o.Text, node.Status, StringComparison.Ordinal))
                  ?? statusOptions[0];

        _originalRemark = _remark;
        _originalStatus = _status;
    }

    public string Code { get; }

    public string Name { get; }

    public string Spec { get; }

    public SubGroupRow Parent { get; }

    /// <summary>下拉框的选项。所有行共用同一份，选中项按引用比较。</summary>
    public IReadOnlyList<SelectOption> StatusOptions { get; }

    /// <summary>列宽。和表头、其它行共用同一份，拖一次列边界全体对齐着动。</summary>
    public TableLayout Layout { get; }

    public bool IsExpanded => true;

    /// <summary>行高。备注是多行输入，行高不够就拖这一行的底边拉大。</summary>
    [ObservableProperty]
    private double _rowHeight = DefaultRowHeight;

    /// <summary>拖行底边时调用。上下限在这里夹住，页面那边只管把位移传进来。</summary>
    public void AdjustRowHeight(double delta)
        => RowHeight = Math.Clamp(RowHeight + delta, MinRowHeight, MaxRowHeight);

    [ObservableProperty]
    private string _remark = string.Empty;

    [ObservableProperty]
    private SelectOption? _status;

    partial void OnRemarkChanged(string value) => MarkEdited();

    partial void OnStatusChanged(SelectOption? value) => MarkEdited();

    /// <summary>改过的行会在最左边亮一条竖线（见页面模板里的那个小 Border）。</summary>
    public bool IsDirty
        => !string.Equals(Remark, _originalRemark, StringComparison.Ordinal)
           || !ReferenceEquals(Status, _originalStatus);

    /// <summary>保存成功后调用：当前值变成新的原值，这一行就不算「已改」了。</summary>
    public void AcceptChanges()
    {
        _originalRemark = Remark;
        _originalStatus = Status;
        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>按当前编辑结果生成可以写回 JSON 的一份。</summary>
    public GroupedItemNode ToNode() => new()
    {
        Code = Code,
        Name = Name,
        Spec = Spec,
        Remark = Remark,
        Status = Status?.Text ?? string.Empty
    };

    private void MarkEdited()
    {
        OnPropertyChanged(nameof(IsDirty));
        _onEdited?.Invoke(this);
    }
}

/// <summary>
/// 多级分组编辑表格示例。
///
/// 演示四件事：JSON 里的两级数据怎么摊到一张表上、分组怎么折叠、
/// 哪几列可编辑以及「改没改过」怎么判断、编辑结果怎么存回 JSON。
/// 数据是模拟的，不连数据库。
/// </summary>
public sealed partial class GroupedTableViewModel : ViewModelBase
{
    private const string SeedFileName = "GroupedTableSample.json";

    /// <summary>编辑结果的存放位置。放在应用自己的数据目录里，不污染程序目录。</summary>
    private static readonly string SavedFilePath =
        Path.Combine(LocalSettings.StorageDirectory, "grouped-table-saved.json");

    private static readonly string[] StatusTexts = ["在用", "待检", "停用"];

    private readonly INotificationService _notify;
    private readonly IDialogService _dialogs;

    private readonly List<MajorGroupRow> _majors = [];
    private readonly List<SelectOption> _statusOptions;

    public GroupedTableViewModel(INotificationService notify, IDialogService dialogs)
    {
        _notify = notify;
        _dialogs = dialogs;

        Title = "分组编辑表格";

        // 选项只建一份，所有行共用：SelectOption 没有重写相等性，
        // ComboBox 的 SelectedItem 比的是引用，各建各的会导致选不中。
        _statusOptions = [.. StatusTexts.Select(text => new SelectOption(text))];
    }

    /// <summary>
    /// 列宽。表头是一次 Grid 布局，每个数据行又是一次，宽度有且只有这一份 ——
    /// 页面上拖动列边界改的就是它。初值：物料编码 / 物料名称 / 规格型号 / 状态，
    /// 备注那列是活动列，宽度由剩余空间算出来，不在这里给。
    /// </summary>
    public TableLayout Layout { get; } = new(150, 150, 150, 130);

    /// <summary>拍平后的可见行，直接绑给 ListView。折叠一次重建一次。</summary>
    [ObservableProperty]
    private IReadOnlyList<IGroupedRow> _rows = [];

    /// <summary>页头那行统计。</summary>
    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>有没有未保存的改动。没改动时保存按钮点不出东西，界面上要给个说明。</summary>
    [ObservableProperty]
    private bool _hasEdits;

    /// <summary>数据是从本机保存的副本读的，还是从种子文件读的。</summary>
    [ObservableProperty]
    private string _dataSourceText = string.Empty;

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        if (IsInitialized) return;

        await RunBusyAsync(LoadAsync, "加载分组数据失败");
        IsInitialized = true;
    }

    /// <summary>点了分组行上的箭头。数据行不响应。</summary>
    public void Toggle(IGroupedRow row)
    {
        switch (row)
        {
            case MajorGroupRow major:
                major.IsExpanded = !major.IsExpanded;
                break;

            case SubGroupRow sub:
                sub.IsExpanded = !sub.IsExpanded;
                break;

            default:
                return;
        }

        RefreshView();
    }

    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var major in _majors)
        {
            major.IsExpanded = true;

            foreach (var sub in major.Subs)
                sub.IsExpanded = true;
        }

        RefreshView();
    }

    /// <summary>
    /// 折叠到「只剩大类」这一层。小类自己的展开状态保留着，
    /// 再把大类展开回来时还是原来的样子。
    /// </summary>
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var major in _majors)
            major.IsExpanded = false;

        RefreshView();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var edited = AllItems().Where(i => i.IsDirty).ToList();

        if (edited.Count == 0)
        {
            _notify.Info("没有改动需要保存。");
            return;
        }

        var json = JsonHelper.Serialize(BuildDocument());

        if (string.IsNullOrWhiteSpace(json))
        {
            _notify.Error("序列化失败，改动没有保存。");
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (!FileHelper.EnsureDirectory(LocalSettings.StorageDirectory)
                || !await FileHelper.WriteTextAsync(SavedFilePath, json))
            {
                throw new BusinessException($"写文件失败：{SavedFilePath}");
            }

            // 存过之后当前值就是新的基准，行上的「已改」标记要跟着清掉，
            // 否则再点一次保存又会把那几行算一遍。
            foreach (var row in AllItems())
                row.AcceptChanges();

            RefreshView();
            DataSourceText = $"数据来源：本机保存的副本（{SavedFilePath}）";
        }, "保存失败");

        _notify.Success($"已保存 {edited.Count} 行改动，下次打开这个页面会读保存后的副本。");
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (HasEdits)
        {
            var confirmed = await _dialogs.ConfirmAsync(
                "重置会丢弃当前所有未保存的修改，并删除本机保存的副本，回到示例数据。\n\n确定继续吗？",
                "重置示例数据",
                primaryText: "重置",
                danger: true);

            if (!confirmed) return;
        }

        await RunBusyAsync(async () =>
        {
            // 不删的话下次进来又会读到旧副本，「重置」就名不副实了。
            if (FileHelper.Exists(SavedFilePath))
                FileHelper.TryDelete(SavedFilePath);

            await LoadAsync();
        }, "重置失败");

        _notify.Info("已回到示例数据。");
    }

    // ---------------------------------------------------------------

    private async Task LoadAsync()
    {
        var path = FileHelper.Exists(SavedFilePath)
            ? SavedFilePath
            : Path.Combine(AppContext.BaseDirectory, "Assets", "Data", SeedFileName);

        var json = await FileHelper.ReadTextAsync(path);

        if (string.IsNullOrWhiteSpace(json))
            throw new BusinessException($"读不到示例数据：{path}");

        var document = JsonHelper.Deserialize<GroupedTableDocument>(json);

        if (document is null || document.Majors.Count == 0)
            throw new BusinessException($"示例数据解析失败或内容为空：{path}");

        BuildTree(document);

        DataSourceText = path == SavedFilePath
            ? $"数据来源：本机保存的副本（{SavedFilePath}）"
            : $"数据来源：示例数据 {SeedFileName}";

        RefreshView();
    }

    /// <summary>
    /// 把 JSON 文档摊成行对象。只在加载时做一次 —— 之后折叠/展开都只是
    /// 重新拍平同一批对象，编辑内容不会因为折叠而丢。
    /// </summary>
    private void BuildTree(GroupedTableDocument document)
    {
        _majors.Clear();

        foreach (var majorNode in document.Majors)
        {
            var major = new MajorGroupRow(majorNode.Name);

            foreach (var subNode in majorNode.Subs)
            {
                var sub = new SubGroupRow(subNode.Name, major);

                foreach (var itemNode in subNode.Items)
                    sub.Items.Add(new GroupedItemRow(itemNode, _statusOptions, sub, Layout, OnItemEdited));

                major.Subs.Add(sub);
            }

            _majors.Add(major);
        }
    }

    /// <summary>按折叠状态把树拍平成一个列表。</summary>
    private void RefreshView()
    {
        var flat = new List<IGroupedRow>();

        foreach (var major in _majors)
        {
            flat.Add(major);

            if (!major.IsExpanded) continue;

            foreach (var sub in major.Subs)
            {
                flat.Add(sub);

                if (sub.IsExpanded)
                    flat.AddRange(sub.Items);
            }
        }

        Rows = flat;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        // 文本框每敲一个字都会走到这里（UpdateSourceTrigger 是 PropertyChanged），
        // 所以只数一遍，别为了写起来好看反复枚举。
        var items = AllItems().ToList();
        var edited = items.Count(i => i.IsDirty);

        SummaryText = $"{_majors.Count} 个大类 · {SubCount()} 个小类 · {items.Count} 条数据"
                      + (edited > 0 ? $" · 未保存的改动 {edited} 条" : " · 暂无改动");

        HasEdits = edited > 0;
    }

    /// <summary>某一行改动后的回调：分组上的统计、页头的统计都要重算。</summary>
    private void OnItemEdited(GroupedItemRow row)
    {
        row.Parent.RefreshCounters();
        row.Parent.Parent.RefreshCounters();
        UpdateSummary();
    }

    private GroupedTableDocument BuildDocument() => new()
    {
        Majors = [.. _majors.Select(major => new GroupedMajorNode
        {
            Name = major.Name,
            Subs = [.. major.Subs.Select(sub => new GroupedSubNode
            {
                Name = sub.Name,
                Items = [.. sub.Items.Select(item => item.ToNode())]
            })]
        })]
    };

    private IEnumerable<GroupedItemRow> AllItems() => _majors.SelectMany(m => m.Subs).SelectMany(s => s.Items);

    private int SubCount() => _majors.Sum(m => m.Subs.Count);
}
