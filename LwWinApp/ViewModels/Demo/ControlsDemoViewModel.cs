using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Services;
using LwWinApp.Utils;

namespace LwWinApp.ViewModels.Demo;

/// <summary>控件示例里的一行排序数据。</summary>
public sealed class SortableRow
{
    public SortableRow(string name, string category, int size, DateTime updatedAt)
    {
        Name = name;
        Category = category;
        Size = size;
        UpdatedAt = updatedAt;
    }

    public string Name { get; }

    public string Category { get; }

    public int Size { get; }

    public DateTime UpdatedAt { get; }

    public string SizeText => StringHelper.FormatSize(Size);

    public string UpdatedText => UpdatedAt.ToFriendlyString();
}

/// <summary>自动完成候选。</summary>
public sealed class SuggestionItem
{
    public SuggestionItem(string text, string description)
    {
        Text = text;
        Description = description;
    }

    public string Text { get; }

    public string Description { get; }
}

/// <summary>
/// 常用控件示例。
///
/// 演示页的原则：每个控件旁边都写清楚「它适合干什么」，
/// 而不只是把控件摆出来。抄代码的人需要知道什么时候该用它。
/// </summary>
public sealed partial class ControlsDemoViewModel : ViewModelBase
{
    private readonly INotificationService _notify;

    /// <summary>表格排序用的原始数据。排序在这份完整数据上做，不在展示结果上做。</summary>
    private readonly List<SortableRow> _allRows;

    public ControlsDemoViewModel(INotificationService notify)
    {
        _notify = notify;
        Title = "常用控件";

        _allRows = BuildSampleRows();
        _rows = _allRows.ToList();

        StatusOptions =
        [
            new SelectOption("启用", EnabledStatus.Enabled),
            new SelectOption("停用", EnabledStatus.Disabled)
        ];
        _selectedStatus = StatusOptions[0];

        GenderOptions =
        [
            new SelectOption("男", Gender.Male),
            new SelectOption("女", Gender.Female),
            new SelectOption("保密", Gender.Unknown)
        ];
        _selectedGender = GenderOptions[2];

        Suggestions =
        [
            new SuggestionItem("Microsoft.UI.Xaml.Controls.NavigationView", "左侧导航容器"),
            new SuggestionItem("Microsoft.UI.Xaml.Controls.TabView", "多标签页容器"),
            new SuggestionItem("Microsoft.UI.Xaml.Controls.InfoBar", "页内消息条"),
            new SuggestionItem("Microsoft.UI.Xaml.Controls.TeachingTip", "引导气泡"),
            new SuggestionItem("Microsoft.UI.Xaml.Controls.NumberBox", "数值输入框"),
            new SuggestionItem("Microsoft.UI.Xaml.Controls.Expander", "可折叠面板")
        ];

        Departments = ["研发中心", "产品部", "运维部", "市场部", "财务部"];
        _selectedDepartment = Departments[0];

        _selectedDate = DateTimeOffset.Now;
        _selectedTime = DateTimeOffset.Now.TimeOfDay;
        _selectedMonth = DateTimeOffset.Now;
        _selectedCalendarDate = DateTimeOffset.Now;
    }

    // ---- 下拉框 ----

    public IReadOnlyList<SelectOption> StatusOptions { get; }

    [ObservableProperty]
    private SelectOption? _selectedStatus;

    public IReadOnlyList<SelectOption> GenderOptions { get; }

    [ObservableProperty]
    private SelectOption? _selectedGender;

    /// <summary>多选下拉用这组。</summary>
    public IReadOnlyList<string> Departments { get; }

    [ObservableProperty]
    private string? _selectedDepartment;

    [ObservableProperty]
    private string _lastAction = "尚未操作";

    // ---- 开关与勾选 ----

    [ObservableProperty]
    private bool _enableFeature = true;

    [ObservableProperty]
    private bool _agreeTerms;

    [ObservableProperty]
    private bool _optionA = true;

    [ObservableProperty]
    private bool _optionB;

    [ObservableProperty]
    private bool _optionC = true;

    [ObservableProperty]
    private bool _canProceed = true;

    /// <summary>三个复选框中选中的数量，用来演示联动。</summary>
    public int CheckedCount => new[] { OptionA, OptionB, OptionC }.Count(v => v);

    /// <summary>
    /// 给用户看的文案。直接绑 <see cref="CheckedCount"/> 只会渲染出一个光秃秃的数字，
    /// 「2」放在复选框下面说明不了任何事情。
    /// </summary>
    public string CheckedCountText => $"已勾选 {CheckedCount} / 3 项";

    partial void OnOptionAChanged(bool value) => NotifyCheckedCount();

    partial void OnOptionBChanged(bool value) => NotifyCheckedCount();

    partial void OnOptionCChanged(bool value) => NotifyCheckedCount();

    private void NotifyCheckedCount()
    {
        OnPropertyChanged(nameof(CheckedCount));
        OnPropertyChanged(nameof(CheckedCountText));
        LastAction = CheckedCountText;
    }

    // ---- 滑块与数值 ----

    [ObservableProperty]
    private double _sliderValue = 40;

    [ObservableProperty]
    private double _numberValue = 8;

    [ObservableProperty]
    private double _ratingValue = 3.5;

    [ObservableProperty]
    private double _opacityValue = 1;

    /// <summary>滑块拖到多少，下面那句提示就跟着变，让数值输入立刻可见。</summary>
    public string SliderHint => SliderValue switch
    {
        < 20 => "几乎不生效",
        < 60 => "温和",
        < 90 => "明显",
        _ => "强烈"
    };

    partial void OnSliderValueChanged(double value) => OnPropertyChanged(nameof(SliderHint));

    // ---- 日期与时间 ----

    [ObservableProperty]
    private DateTimeOffset? _selectedDate;

    [ObservableProperty]
    private TimeSpan? _selectedTime;

    [ObservableProperty]
    private DateTimeOffset? _selectedMonth;

    [ObservableProperty]
    private DateTimeOffset? _selectedCalendarDate;

    public string DateSummary
    {
        get
        {
            var date = SelectedDate?.ToString("yyyy-MM-dd") ?? "未选";
            var time = SelectedTime is { } t ? $"{t.Hours:00}:{t.Minutes:00}" : "未选";

            return $"{date} {time}";
        }
    }

    partial void OnSelectedDateChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(DateSummary));

    partial void OnSelectedTimeChanged(TimeSpan? value) => OnPropertyChanged(nameof(DateSummary));

    // ---- 搜索建议 ----

    public IReadOnlyList<SuggestionItem> Suggestions { get; }

    [ObservableProperty]
    private SuggestionItem? _selectedSuggestion;

    /// <summary>AutoSuggestBox 的文本变化回调。做成方法而不是命令 ——
    /// 它需要同步拿到用户输入的当前文本，命令的异步边界反而碍事。</summary>
    public void UpdateSuggestionQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            SuggestionBoxItems = [];
            return;
        }

        SuggestionBoxItems = Suggestions
            .Where(s => s.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || s.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        LastAction = $"搜索「{query}」，命中 {SuggestionBoxItems.Count} 条";
    }

    [ObservableProperty]
    private IReadOnlyList<SuggestionItem> _suggestionBoxItems = [];

    public void OnSuggestionChosen(SuggestionItem? item)
    {
        if (item is not null)
            LastAction = $"选中建议：{item.Text}";
    }

    // ---- 表格（自建：表头排序 + 交替行色 + 导出）----

    [ObservableProperty]
    private IReadOnlyList<SortableRow> _rows = [];

    [ObservableProperty]
    private SortableRow? _selectedRow;

    [ObservableProperty]
    private string? _sortColumn;

    [ObservableProperty]
    private bool _sortDescending;

    partial void OnSelectedRowChanged(SortableRow? value)
        => LastAction = value is null ? "已取消选中" : $"选中行：{value.Name}";

    /// <summary>点击表头排序。再点同一列就反向。</summary>
    [RelayCommand]
    private void Sort(string? column)
    {
        if (string.IsNullOrWhiteSpace(column)) return;

        if (string.Equals(SortColumn, column, StringComparison.Ordinal))
            SortDescending = !SortDescending;
        else
        {
            SortColumn = column;
            SortDescending = false;
        }

        ApplySort();
    }

    public string NameSortGlyph => SortGlyphFor("name");
    public string CategorySortGlyph => SortGlyphFor("category");
    public string SizeSortGlyph => SortGlyphFor("size");
    public string UpdatedSortGlyph => SortGlyphFor("updated");

    private string SortGlyphFor(string column)
        => !string.Equals(SortColumn, column, StringComparison.Ordinal)
            ? ""                      // 未排序：一个向下的箭头，提示「可点」
            : SortDescending ? "" : "";   // 降序 / 升序

    partial void OnSortColumnChanged(string? value) => RaiseSortGlyphs();

    partial void OnSortDescendingChanged(bool value) => RaiseSortGlyphs();

    private void RaiseSortGlyphs()
    {
        OnPropertyChanged(nameof(NameSortGlyph));
        OnPropertyChanged(nameof(CategorySortGlyph));
        OnPropertyChanged(nameof(SizeSortGlyph));
        OnPropertyChanged(nameof(UpdatedSortGlyph));
    }

    private void ApplySort()
    {
        if (string.IsNullOrWhiteSpace(SortColumn))
        {
            Rows = _allRows.ToList();
            return;
        }

        // 每列的比较方式不同，用 switch 表达到位，别硬塞进一个 lambda。
        IOrderedEnumerable<SortableRow> sorted = SortColumn switch
        {
            "category" => Order(_allRows, r => r.Category),
            "size" => Order(_allRows, r => r.Size),
            "updated" => Order(_allRows, r => r.UpdatedAt),
            _ => Order(_allRows, r => r.Name)
        };

        Rows = sorted.ToList();

        IOrderedEnumerable<SortableRow> Order<TKey>(IEnumerable<SortableRow> source, Func<SortableRow, TKey> key)
            => SortDescending ? source.OrderByDescending(key) : source.OrderBy(key);
    }

    [RelayCommand]
    private async Task ExportRowsAsync()
    {
        var exported = await RunBusyAsync(async () =>
        {
            var path = await CsvExporter.ExportAsync(
                Rows,
                [
                    ("名称", r => r.Name),
                    ("分类", r => r.Category),
                    ("大小", r => r.SizeText),
                    ("更新时间", r => r.UpdatedAt)
                ],
                $"控件示例表格_{DateTimeHelper.ForFileName()}");

            return (Count: Rows.Count, Path: path);
        }, "导出失败");

        if (!exported.HasValue) return;

        if (exported.Value.Path is null)
        {
            _notify.Warning("导出失败：无法写入导出目录。");
            return;
        }

        _notify.Success($"已导出 {exported.Value.Count} 行到 {exported.Value.Path}");
        FileHelper.RevealInExplorer(exported.Value.Path);
    }

    // ---- 各种小动作，用来把「有反馈」这件事演示清楚 ----

    [RelayCommand]
    private void RunAction(string? name) => LastAction = $"{name ?? "操作"}：{DateTime.Now:HH:mm:ss}";

    [RelayCommand]
    private void ResetAll()
    {
        SelectedStatus = StatusOptions[0];
        SelectedGender = GenderOptions[2];
        SelectedDepartment = Departments[0];
        EnableFeature = true;
        AgreeTerms = false;
        OptionA = true;
        OptionB = false;
        OptionC = true;
        SliderValue = 40;
        NumberValue = 8;
        RatingValue = 3.5;
        OpacityValue = 1;
        SelectedDate = DateTimeOffset.Now;
        SelectedTime = DateTimeOffset.Now.TimeOfDay;
        SelectedRow = null;
        SortColumn = null;
        SortDescending = false;

        ApplySort();
        LastAction = "已恢复默认值";
    }

    private static List<SortableRow> BuildSampleRows()
    {
        string[] names =
        [
            "NavigationView.xaml", "TabView.xaml", "InfoBar.xaml", "TeachingTip.xaml",
            "NumberBox.xaml", "Expander.xaml", "TreeView.xaml", "ComboBox.xaml",
            "DataGrid.xaml", "Pager.xaml", "BusyOverlay.xaml", "PageHeader.xaml"
        ];

        string[] categories = ["布局", "导航", "反馈", "输入", "数据"];

        var random = new Random(20260101);   // 固定种子，每次打开顺序一致
        var baseline = DateTime.Today.AddDays(-30);

        return names
            .Select((name, index) => new SortableRow(
                name,
                categories[index % categories.Length],
                random.Next(1024, 512 * 1024),
                baseline.AddHours(index * 37).AddMinutes(random.Next(0, 60))))
            .ToList();
    }
}
