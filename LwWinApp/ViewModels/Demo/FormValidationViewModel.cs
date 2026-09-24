using System.ComponentModel.DataAnnotations;
using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Services;
using LwWinApp.Utils;

namespace LwWinApp.ViewModels.Demo;

/// <summary>
/// 表单数据模型。
///
/// 单独抽一个类、而不是把这些属性摊在 ViewModel 上，是因为校验需要继承
/// <see cref="ObservableValidator"/>，而 ViewModelBase 已经继承了 ObservableObject ——
/// C# 单继承，两者只能选一个。分成「Model 负责校验、ViewModel 负责流程」反而更清楚：
/// 校验规则跟字段写在一起，一眼能对上。
/// </summary>
public sealed partial class RegistrationForm : ObservableValidator
{
    [ObservableProperty]
    [Required(ErrorMessage = "登录账号不能为空")]
    [MinLength(3, ErrorMessage = "登录账号至少 3 个字符")]
    [MaxLength(20, ErrorMessage = "登录账号最多 20 个字符")]
    [RegularExpression(RegexHelper.UserNameText, ErrorMessage = "只能包含字母、数字和下划线，且以字母开头")]
    private string _loginName = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "姓名不能为空")]
    [MaxLength(30, ErrorMessage = "姓名最多 30 个字符")]
    private string _realName = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    private string _email = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "手机号不能为空")]
    [RegularExpression(RegexHelper.PhoneText, ErrorMessage = "手机号格式不正确")]
    private string _phone = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "密码不能为空")]
    [MinLength(6, ErrorMessage = "密码至少 6 位")]
    private string _password = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "请再次输入密码")]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [Range(typeof(bool), "true", "true", ErrorMessage = "必须先同意服务条款")]
    private bool _acceptTerms;

    /// <summary>
    /// 年龄用 double 而不是 int：NumberBox.Value 是 double，
    /// x:Bind 不会自动做数值转换，类型对不上会直接编译不过。
    /// </summary>
    [ObservableProperty]
    [Range(1d, 120d, ErrorMessage = "年龄需在 1 ~ 120 之间")]
    private double _age = 25;

    // ---- 给 ViewModel 用的入口 ----
    // ValidateAllProperties / ClearErrors 在 CommunityToolkit.Mvvm 里是 protected，
    // 只有子类能调；对外开两个口子，规则和调用方就能各就各位。

    /// <summary>手动跑一遍所有字段的校验。</summary>
    public void ValidateAll() => ValidateAllProperties();

    /// <summary>清掉某个字段的错误，不传则全清。</summary>
    public void ClearFieldErrors(string? propertyName = null) => ClearErrors(propertyName);
}

/// <summary>
/// 表单校验示例。
///
/// 校验分两层，缺一不可：
/// <list type="number">
/// <item>界面层：字段失去焦点或提交时立刻给红字提示，用户不用等到最后才知道哪里错了</item>
/// <item>业务层：提交时再用 Service 校验一遍（用户名是否重复、两次密码是否一致）</item>
/// </list>
/// 只有第一层会被绕过，只有第二层体验很差。
/// </summary>
public sealed partial class FormValidationViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notify;

    public FormValidationViewModel(IDialogService dialogs, INotificationService notify)
    {
        _dialogs = dialogs;
        _notify = notify;

        Title = "表单校验";

        Form.ErrorsChanged += (_, _) =>
        {
            // 错误提示、是否有错、错误汇总都从 Form 的状态算出来，变化时统一通知一遍。
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(ErrorSummary));
            OnPropertyChanged(nameof(LoginNameError));
            OnPropertyChanged(nameof(RealNameError));
            OnPropertyChanged(nameof(EmailError));
            OnPropertyChanged(nameof(PhoneError));
            OnPropertyChanged(nameof(PasswordError));
            OnPropertyChanged(nameof(ConfirmPasswordError));
            OnPropertyChanged(nameof(AcceptTermsError));
            OnPropertyChanged(nameof(AgeError));
        };
    }

    public RegistrationForm Form { get; } = new();

    // ---- 每个字段的错误提示 ----

    // 单独暴露成属性而不是让 XAML 去遍历 GetErrors()：x:Bind 不支持那种写法，
    // 而转换器又拿不到「哪个字段」这个上下文。
    public string? LoginNameError => ErrorOf(nameof(RegistrationForm.LoginName));

    public string? RealNameError => ErrorOf(nameof(RegistrationForm.RealName));

    public string? EmailError => ErrorOf(nameof(RegistrationForm.Email));

    public string? PhoneError => ErrorOf(nameof(RegistrationForm.Phone));

    public string? PasswordError => ErrorOf(nameof(RegistrationForm.Password));

    public string? ConfirmPasswordError => ErrorOf(nameof(RegistrationForm.ConfirmPassword));

    public string? AcceptTermsError => ErrorOf(nameof(RegistrationForm.AcceptTerms));

    public string? AgeError => ErrorOf(nameof(RegistrationForm.Age));

    private string? ErrorOf(string propertyName)
        => Form.GetErrors(propertyName).FirstOrDefault()?.ErrorMessage;

    /// <summary>是否显示校验摘要。点了提交之后才显示，免得一进页面就满屏红。</summary>
    [ObservableProperty]
    private bool _showErrors;

    [ObservableProperty]
    private string? _submittedSummary;

    /// <summary>提交成功提示是否可见。InfoBar.IsOpen 是 bool，不能直接绑一个可空字符串。</summary>
    public bool HasSubmitted => !string.IsNullOrWhiteSpace(SubmittedSummary);

    partial void OnSubmittedSummaryChanged(string? value) => OnPropertyChanged(nameof(HasSubmitted));

    public bool HasErrors => Form.HasErrors;

    /// <summary>
    /// 所有字段的错误汇总。用 <c>GetErrors()</c> 而不是自己拼字符串，
    /// 保证摘要和字段旁边的红字永远一致。
    /// </summary>
    public string ErrorSummary
    {
        get
        {
            var messages = Form.GetErrors()
                .SelectMany(result => result.MemberNames.Select(name => (Name: name, Message: result.ErrorMessage)))
                .Where(item => item.Message is not null)
                .DistinctBy(item => item.Name)
                .Select(item => $"• {FieldLabel(item.Name)}：{item.Message}")
                .ToList();

            return messages.Count == 0 ? string.Empty : string.Join("\n", messages);
        }
    }

    /// <summary>字段名转成中文标签，直接显示属性名太难看了。</summary>
    private static string FieldLabel(string propertyName) => propertyName switch
    {
        nameof(RegistrationForm.LoginName) => "登录账号",
        nameof(RegistrationForm.RealName) => "姓名",
        nameof(RegistrationForm.Email) => "邮箱",
        nameof(RegistrationForm.Phone) => "手机号",
        nameof(RegistrationForm.Password) => "密码",
        nameof(RegistrationForm.ConfirmPassword) => "确认密码",
        nameof(RegistrationForm.AcceptTerms) => "服务条款",
        nameof(RegistrationForm.Age) => "年龄",
        _ => propertyName
    };

    /// <summary>提交。先跑数据注解校验，再跑「两次密码是否一致」这类跨字段校验。</summary>
    [RelayCommand]
    private async Task SubmitAsync()
    {
        ShowErrors = true;

        // 触发所有字段的校验。属性 setter 只在值变化时校验，
        // 用户原样提交（没改过任何字段）时得手动跑一次。
        Form.ValidateAll();

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ErrorSummary));

        if (Form.HasErrors)
        {
            await _dialogs.ShowWarningAsync(
                "表单里还有未通过校验的字段，请按提示修改后重试。",
                "校验未通过");
            return;
        }

        // 跨字段校验：单个字段都合法，但它们之间的关系不合法时只有这里能发现。
        if (!string.Equals(Form.Password, Form.ConfirmPassword, StringComparison.Ordinal))
        {
            await _dialogs.ShowErrorAsync("两次输入的密码不一致。", "校验未通过");
            return;
        }

        if (!RegexHelper.IsStrongPassword(Form.Password))
        {
            await _dialogs.ShowErrorAsync("密码至少 6 位，且需同时包含字母和数字。", "密码强度不足");
            return;
        }

        SubmittedSummary =
            $"提交成功：{Form.RealName}（{Form.LoginName}）· {StringHelper.MaskEmail(Form.Email)} · " +
            $"{StringHelper.MaskPhone(Form.Phone)} · {Form.Age} 岁";

        _notify.Success("表单已提交并通过全部校验。");
    }

    [RelayCommand]
    private void Reset()
    {
        Form.LoginName = string.Empty;
        Form.RealName = string.Empty;
        Form.Email = string.Empty;
        Form.Phone = string.Empty;
        Form.Password = string.Empty;
        Form.ConfirmPassword = string.Empty;
        Form.AcceptTerms = false;
        Form.Age = 25;

        Form.ClearFieldErrors();

        ShowErrors = false;
        SubmittedSummary = null;

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ErrorSummary));

        _notify.Info("表单已重置。");
    }

    /// <summary>一键填入一组非法数据，方便快速看到各种校验提示。</summary>
    [RelayCommand]
    private async Task FillInvalidAsync()
    {
        Form.LoginName = "1_ab";          // 数字开头 → 账号格式错
        Form.RealName = string.Empty;      // 空 → 必填错
        Form.Email = "not-an-email";       // 格式错
        Form.Phone = "12345";              // 位数不够
        Form.Password = "abc";             // 太短
        Form.ConfirmPassword = "abcd";     // 与密码不一致

        ShowErrors = true;
        Form.ValidateAll();

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ErrorSummary));

        await _dialogs.ShowInfoAsync(
            "已填入一组非法数据，可以直接看到各字段的校验提示。",
            "演示数据");
    }
}
