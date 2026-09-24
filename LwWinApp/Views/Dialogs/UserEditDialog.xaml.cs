using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Models.Entities;
using LwWinApp.Services;
using LwWinApp.Utils;

namespace LwWinApp.Views.Dialogs;

/// <summary>
/// 用户新增 / 编辑对话框。
///
/// 保存逻辑放在对话框里而不是调用方，是刻意的：
/// 校验失败时对话框不关，用户刚填的内容一个字都不会丢。
/// 如果先关掉再保存，一旦失败就得让用户从头再填一遍。
/// </summary>
public sealed partial class UserEditDialog : ContentDialog
{
    private readonly IUserService _users;
    private readonly SysUser _current;
    private readonly bool _isCreate;

    private bool _saving;

    public UserEditDialog(
        IUserService users,
        SysUser? existing,
        IReadOnlyList<SelectOption> deptOptions,
        IReadOnlyList<SelectOption> roleOptions)
    {
        _users = users;
        _isCreate = existing is null;
        _current = existing ?? new SysUser();

        // Model 必须在 InitializeComponent 之前建好：
        // x:Bind 是在 InitializeComponent 里求值的，Model 还是 null 的话会直接崩。
        Model = new UserEditModel(_current, _isCreate, deptOptions, roleOptions);

        InitializeComponent();

        Title = _isCreate ? "新增用户" : $"编辑用户 · {_current.UserName}";
        PrimaryButtonText = _isCreate ? "创建" : "保存";

        // 账号一旦创建就不再允许修改：审计日志里记的是当时的账号名，
        // 允许改名会让历史记录对不上人。
        UserNameBox.IsReadOnly = !_isCreate;

        if (!_isCreate)
            ToolTipService.SetToolTip(UserNameBox, "账号创建后不可修改");

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    public UserEditModel Model { get; }

    /// <summary>保存是否成功。调用方据此决定要不要刷新列表。</summary>
    public bool Saved { get; private set; }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 先取消关闭，是否关掉由保存结果决定。
        args.Cancel = true;

        if (_saving) return;

        if (Model.ValidateLocally() is { Count: > 0 } localErrors)
        {
            ShowError(string.Join("；", localErrors) + "。");
            return;
        }

        _saving = true;
        IsPrimaryButtonEnabled = false;

        try
        {
            var entity = Model.ToEntity(_current);

            if (_isCreate)
                await _users.CreateAsync(entity, Model.Password);
            else
                await _users.UpdateAsync(entity, Model.Password);

            Saved = true;
            Hide();
        }
        catch (BusinessException ex)
        {
            // 业务校验失败：这是用户能看懂、能自己改的问题，就地展示。
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

/// <summary>对话框上表单的绑定模型。</summary>
public sealed partial class UserEditModel : ObservableObject
{
    private readonly bool _isCreate;

    public UserEditModel(
        SysUser source,
        bool isCreate,
        IReadOnlyList<SelectOption> deptOptions,
        IReadOnlyList<SelectOption> roleOptions)
    {
        _isCreate = isCreate;

        DeptOptions = deptOptions;
        RoleOptions = roleOptions;

        GenderOptions =
        [
            new SelectOption("未知", Gender.Unknown),
            new SelectOption("男", Gender.Male),
            new SelectOption("女", Gender.Female)
        ];

        StatusOptions =
        [
            new SelectOption("启用", EnabledStatus.Enabled),
            new SelectOption("停用", EnabledStatus.Disabled)
        ];

        // 从实体反向填入表单。编辑时账号不允许改（改账号会让审计日志对不上人），
        // 所以这里保留原值只是为了显示。
        _loginName = source.UserName;
        _realName = source.RealName;
        _email = source.Email;
        _phone = source.Phone;
        _remark = source.Remark;

        _selectedGender = GenderOptions.FirstOrDefault(o => o.AsEnum<Gender>() == source.Gender) ?? GenderOptions[0];
        _selectedStatus = StatusOptions.FirstOrDefault(o => o.AsEnum<EnabledStatus>() == source.Status) ?? StatusOptions[0];
        _selectedDept = DeptOptions.FirstOrDefault(o => o.AsLong() == source.DeptId) ?? DeptOptions.FirstOrDefault();
        _selectedRole = RoleOptions.FirstOrDefault(o => o.AsLong() == source.RoleId) ?? RoleOptions.FirstOrDefault();
    }

    public IReadOnlyList<SelectOption> GenderOptions { get; }

    public IReadOnlyList<SelectOption> StatusOptions { get; }

    public IReadOnlyList<SelectOption> DeptOptions { get; }

    public IReadOnlyList<SelectOption> RoleOptions { get; }

    /// <summary>新增时是「密码」，编辑时是「新密码（留空则不修改）」。</summary>
    public string PasswordLabel => _isCreate ? "密码 *" : "新密码（留空则不修改）";

    [ObservableProperty]
    private string _loginName = string.Empty;

    [ObservableProperty]
    private string _realName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _remark;

    [ObservableProperty]
    private SelectOption? _selectedGender;

    [ObservableProperty]
    private SelectOption? _selectedStatus;

    [ObservableProperty]
    private SelectOption? _selectedDept;

    [ObservableProperty]
    private SelectOption? _selectedRole;

    /// <summary>
    /// 本地能判定的校验，返回全部问题（空列表表示通过）。
    ///
    /// 一次性返回所有错误而不是遇到第一个就返回：否则用户得「改一个、提交一次」
    /// 来回好几轮，明明一眼能看完的问题被拆成好几次往返。
    /// 唯一性之类的仍然要交给 Service——那才是权威。
    /// </summary>
    public IReadOnlyList<string> ValidateLocally()
    {
        var errors = new List<string>();

        if (_isCreate)
        {
            // 账号是登录凭据，空值必须在这里挡住：数据库里 user_name 是 NOT NULL，
            // 但空字符串能通过，真让它落库就会出现一个谁都登不上去的账号。
            if (string.IsNullOrWhiteSpace(LoginName))
                errors.Add("请填写登录账号");
            else if (!RegexHelper.IsUserName(LoginName.Trim()))
                errors.Add("登录账号需字母开头、3~20 位字母数字或下划线");
        }

        if (string.IsNullOrWhiteSpace(RealName))
            errors.Add("请填写姓名");

        if (_isCreate && string.IsNullOrWhiteSpace(Password))
        {
            errors.Add("请填写密码");
        }
        else if (!string.IsNullOrWhiteSpace(Password) && !RegexHelper.IsStrongPassword(Password))
        {
            // 编辑时密码留空表示不修改，所以只有填了才校验强度。
            errors.Add("密码至少 6 位且同时含字母和数字");
        }

        if (!string.IsNullOrWhiteSpace(Email) && !RegexHelper.IsEmail(Email.Trim()))
            errors.Add("邮箱格式不正确");

        if (!string.IsNullOrWhiteSpace(Phone) && !RegexHelper.IsPhone(Phone.Trim()))
            errors.Add("手机号格式不正确");

        return errors;
    }

    /// <summary>把表单值写回实体。传入的是原实体或新建的实例，Id 等字段由它带过来。</summary>
    public SysUser ToEntity(SysUser target)
    {
        // 账号只在新增时写入，编辑时表单里那个框是只读的。
        if (_isCreate)
            target.UserName = LoginName.Trim();

        target.RealName = RealName.Trim();
        target.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        target.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        target.Remark = string.IsNullOrWhiteSpace(Remark) ? null : Remark.Trim();
        target.Gender = SelectedGender.AsEnum<Gender>() ?? Gender.Unknown;
        target.Status = SelectedStatus.AsEnum<EnabledStatus>() ?? EnabledStatus.Enabled;
        target.DeptId = SelectedDept.AsLong();
        target.RoleId = SelectedRole.AsLong();

        return target;
    }
}
