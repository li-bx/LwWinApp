using LwWinApp.ViewModels;
using LwWinApp.ViewModels.Demo;

namespace LwWinApp.Views.Demo;

/// <summary>常用控件示例页。</summary>
public sealed partial class ControlsDemoPage : PageBase
{
    public ControlsDemoPage()
    {
        InitializeComponent();

        // 页面由 XAML 体系创建（Frame.Navigate 只认无参构造），拿不到构造注入，
        // 只能从容器里取。容器里没有就说明漏注册了，Require 会直接抛。
        Vm = Require<ControlsDemoViewModel>();
    }

    /// <summary>强类型的 ViewModel。x:Bind 需要具体类型才能生成绑定代码，接口类型不行。</summary>
    public ControlsDemoViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;

    private void OnSuggestionTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // 只在用户输入时更新候选。程序设值和选中建议也会触发这个事件，
        // 不过滤掉的话，选中一条建议会立刻把候选列表换掉。
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        Vm.UpdateSuggestionQuery(sender.Text);
    }

    private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        Vm.SelectedSuggestion = args.SelectedItem as SuggestionItem;
        Vm.OnSuggestionChosen(Vm.SelectedSuggestion);
    }

    private void OnCalendarDateChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
    {
        var date = sender.SelectedDates.FirstOrDefault();
        Vm.SelectedCalendarDate = date == default ? null : date;
    }

    private void OnShowTeachingTip(object sender, RoutedEventArgs e)
    {
        if (!DemoTip.IsOpen)
        {
            DemoTip.IsOpen = true;
            return;
        }

        // 已经开着的时候直接再置 true 是没有效果的（值没变化，不触发通知）。
        // 先关掉，等这一帧的布局跑完再开，才能真正重新弹出。
        DemoTip.IsOpen = false;
        DispatcherQueue.TryEnqueue(() => DemoTip.IsOpen = true);
    }
}
