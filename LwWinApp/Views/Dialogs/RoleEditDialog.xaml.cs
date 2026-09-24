using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Services;

namespace LwWinApp.Views.Dialogs;

/// <summary>角色新增 / 编辑对话框。保存逻辑同 <see cref="UserEditDialog"/>：失败不关闭。</summary>
public sealed partial class RoleEditDialog : ContentDialog
{
    private readonly IRoleService _roles;
    private readonly SysRole _current;
    private readonly bool _isCreate;

    private bool _saving;

    public RoleEditDialog(IRoleService roles, SysRole? existing)
    {
        _roles = roles;
        _isCreate = existing is null;
        _current = existing ?? new SysRole();

        Model = new RoleEditModel(_current, _isCreate);

        InitializeComponent();

        Title = _isCreate ? "新增角色" : $"编辑角色 · {_current.RoleName}";
        PrimaryButtonText = _isCreate ? "创建" : "保存";

        // 角色编码会出现在权限判断和历史日志里，创建后不再允许改动。
        CodeBox.IsReadOnly = !_isCreate;

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    public RoleEditModel Model { get; }

    public bool Saved { get; private set; }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;

        if (_saving) return;

        if (string.IsNullOrWhiteSpace(Model.RoleName))
        {
            ShowError("请填写角色名称。");
            return;
        }

        _saving = true;
        IsPrimaryButtonEnabled = false;

        try
        {
            var entity = Model.ToEntity(_current);

            if (_isCreate)
                await _roles.CreateAsync(entity);
            else
                await _roles.UpdateAsync(entity);

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

/// <summary>角色表单的绑定模型。</summary>
public sealed partial class RoleEditModel : ObservableObject
{
    private readonly bool _isCreate;

    public RoleEditModel(SysRole source, bool isCreate)
    {
        _isCreate = isCreate;

        StatusOptions =
        [
            new SelectOption("启用", EnabledStatus.Enabled),
            new SelectOption("停用", EnabledStatus.Disabled)
        ];

        _roleCode = source.RoleCode;
        _roleName = source.RoleName;
        _remark = source.Remark;

        // NumberBox 绑的是 double，这里也用 double，免得 x:Bind 在两个数值类型之间来回转。
        _sort = source.Sort;

        _selectedStatus = StatusOptions.FirstOrDefault(o => o.AsEnum<EnabledStatus>() == source.Status)
                          ?? StatusOptions[0];
    }

    public IReadOnlyList<SelectOption> StatusOptions { get; }

    [ObservableProperty]
    private string _roleCode = string.Empty;

    [ObservableProperty]
    private string _roleName = string.Empty;

    [ObservableProperty]
    private double _sort;

    [ObservableProperty]
    private string? _remark;

    [ObservableProperty]
    private SelectOption? _selectedStatus;

    public SysRole ToEntity(SysRole target)
    {
        if (_isCreate)
            target.RoleCode = RoleCode.Trim();

        target.RoleName = RoleName.Trim();
        target.Remark = string.IsNullOrWhiteSpace(Remark) ? null : Remark.Trim();
        target.Sort = (int)Math.Max(0, Sort);
        target.Status = SelectedStatus.AsEnum<EnabledStatus>() ?? EnabledStatus.Enabled;

        return target;
    }
}
