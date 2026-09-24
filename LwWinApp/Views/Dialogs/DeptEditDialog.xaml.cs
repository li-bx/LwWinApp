using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Services;

namespace LwWinApp.Views.Dialogs;

/// <summary>部门新增 / 编辑对话框。</summary>
public sealed partial class DeptEditDialog : ContentDialog
{
    private readonly IDeptService _depts;
    private readonly SysDept _current;
    private readonly bool _isCreate;

    private bool _saving;

    public DeptEditDialog(
        IDeptService depts,
        SysDept? existing,
        long defaultParentId,
        IReadOnlyList<SelectOption> parentOptions)
    {
        _depts = depts;
        _isCreate = existing is null;
        _current = existing ?? new SysDept();

        Model = new DeptEditModel(_current, _isCreate, defaultParentId, parentOptions);

        InitializeComponent();

        Title = _isCreate
            ? (defaultParentId > 0 ? "新增下级部门" : "新增部门")
            : $"编辑部门 · {_current.DeptName}";

        PrimaryButtonText = _isCreate ? "创建" : "保存";

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    public DeptEditModel Model { get; }

    public bool Saved { get; private set; }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;

        if (_saving) return;

        if (string.IsNullOrWhiteSpace(Model.DeptName))
        {
            ShowError("请填写部门名称。");
            return;
        }

        _saving = true;
        IsPrimaryButtonEnabled = false;

        try
        {
            var entity = Model.ToEntity(_current);

            if (_isCreate)
                await _depts.CreateAsync(entity);
            else
                await _depts.UpdateAsync(entity);

            Saved = true;
            Hide();
        }
        catch (BusinessException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            ShowError($"保存失败：{ex.Message}");
        }
        finally
        {
            _saving = false;
            IsPrimaryButtonEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }
}

/// <summary>部门表单的绑定模型。</summary>
public sealed partial class DeptEditModel : ObservableObject
{
    private readonly bool _isCreate;

    public DeptEditModel(
        SysDept source,
        bool isCreate,
        long defaultParentId,
        IReadOnlyList<SelectOption> parentOptions)
    {
        _isCreate = isCreate;
        ParentOptions = parentOptions;

        StatusOptions =
        [
            new SelectOption("启用", EnabledStatus.Enabled),
            new SelectOption("停用", EnabledStatus.Disabled)
        ];

        _deptName = source.DeptName;
        _leader = source.Leader;
        _sort = source.Sort;

        var targetParent = isCreate ? defaultParentId : source.ParentId;

        _selectedParent = ParentOptions.FirstOrDefault(o => o.AsLong() == targetParent)
                          ?? ParentOptions.FirstOrDefault();

        _selectedStatus = StatusOptions.FirstOrDefault(o => o.AsEnum<EnabledStatus>() == source.Status)
                          ?? StatusOptions[0];
    }

    public IReadOnlyList<SelectOption> ParentOptions { get; }

    public IReadOnlyList<SelectOption> StatusOptions { get; }

    [ObservableProperty]
    private string _deptName = string.Empty;

    [ObservableProperty]
    private string? _leader;

    [ObservableProperty]
    private double _sort;

    [ObservableProperty]
    private SelectOption? _selectedParent;

    [ObservableProperty]
    private SelectOption? _selectedStatus;

    public SysDept ToEntity(SysDept target)
    {
        target.ParentId = SelectedParent.AsLong() ?? 0;
        target.DeptName = DeptName.Trim();
        target.Leader = string.IsNullOrWhiteSpace(Leader) ? null : Leader.Trim();
        target.Sort = (int)Math.Max(0, Sort);
        target.Status = SelectedStatus.AsEnum<EnabledStatus>() ?? EnabledStatus.Enabled;

        return target;
    }
}
