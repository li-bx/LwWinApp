using LwWinApp.ViewModels;
using LwWinApp.ViewModels.Demo;

namespace LwWinApp.Views.Demo;

/// <summary>表单校验示例页。</summary>
public sealed partial class FormValidationPage : PageBase
{
    public FormValidationPage()
    {
        InitializeComponent();
        Vm = Require<FormValidationViewModel>();
    }

    public FormValidationViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;

    /// <summary>
    /// PasswordBox 的 Password 属性不是依赖属性，x:Bind 的 TwoWay 要等失焦才回写。
    /// 想边输边校验，就得用 PasswordChanged 手动推一次。
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox box) return;

        if (!string.Equals(Vm.Form.Password, box.Password, StringComparison.Ordinal))
            Vm.Form.Password = box.Password;
    }

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox box) return;

        if (!string.Equals(Vm.Form.ConfirmPassword, box.Password, StringComparison.Ordinal))
            Vm.Form.ConfirmPassword = box.Password;
    }
}
