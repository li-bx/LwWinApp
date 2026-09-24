using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Services;
using LwWinApp.Views.Demo.Bodies;
using LwWinApp.Views.Windows;

namespace LwWinApp.ViewModels.Demo;

/// <summary>
/// 弹窗窗口示例。
///
/// <para>
/// 和「弹出对话框」那一页的区别在窗口本身：那边是 ContentDialog，挂在主窗口里的一块浮层；
/// 这里是货真价实的窗口，可以有自己的尺寸、能被拖动、能和非模态地长期开着。
/// 打开/关闭的规矩都由 <see cref="IChildWindowService"/> 管，ViewModel 只负责
/// 「造一个窗口、往里面塞什么、拿到结果之后做什么」。
/// </para>
///
/// <para>
/// 窗口的构造放在这里（而不是服务里）是有意的：服务不该认识业务，页面才认识。
/// 这跟系统里 <c>UserEditDialog</c> 那些对话框是同一个套路 ——
/// ViewModel <c>new</c> 出来交给服务显示，服务负责显示这件事本身。
/// </para>
/// </summary>
public sealed partial class WindowDemoViewModel : ViewModelBase
{
    private readonly IChildWindowService _windows;
    private readonly INotificationService _notify;

    /// <summary>非模态窗口的编号，只为了让人一眼看出「同时开了好几个」。</summary>
    private int _modelessSequence;

    public WindowDemoViewModel(IChildWindowService windows, INotificationService notify)
    {
        _windows = windows;
        _notify = notify;

        Title = "弹窗窗口";

        PriorityOptions =
        [
            new SelectOption("普通", 0),
            new SelectOption("较高", 1),
            new SelectOption("紧急", 2)
        ];

        ScopeOptions =
        [
            new SelectOption("单个用户", 0),
            new SelectOption("某个部门", 1),
            new SelectOption("全公司", 2)
        ];

        // 服务是单例、ViewModel 是每个标签一份，事件要先订后退（见 OnTabClosed）。
        // 另外这里必须先取一次当前值：切走这个标签再切回来时，新 ViewModel 还开着旧窗口。
        _windows.ModelessWindowCountChanged += OnModelessCountChanged;
        ModelessCount = _windows.ModelessWindowCount;
    }

    /// <summary>最近一次操作的结果。</summary>
    [ObservableProperty]
    private string _lastResult = "还没有打开过任何窗口。";

    /// <summary>当前还开着的非模态窗口数量。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModelessSummary))]
    [NotifyCanExecuteChangedFor(nameof(CloseAllModelessCommand))]
    private int _modelessCount;

    /// <summary>非模态窗口数量的说明文字。直接在界面上拼字符串会把格式散得到处都是。</summary>
    public string ModelessSummary => ModelessCount == 0
        ? "当前没有打开的非模态窗口。"
        : $"当前已打开 {ModelessCount} 个非模态窗口 —— 主窗口最小化时它们会一起收起来。";

    public IReadOnlyList<SelectOption> PriorityOptions { get; }

    public IReadOnlyList<SelectOption> ScopeOptions { get; }

    public override void OnTabClosed()
    {
        // 不退订的话，这个页面关掉之后服务还攥着它的事件处理器 ——
        // 用户每关一个非模态窗口都会往一个已经没人看的 ViewModel 上写一次属性。
        _windows.ModelessWindowCountChanged -= OnModelessCountChanged;
    }

    // ---- 模态窗口 ----

    /// <summary>只有「知道了」一个按钮的消息窗口。</summary>
    [RelayCommand]
    private async Task ShowMessageWindowAsync()
    {
        var window = new DialogWindow
        {
            Title = "连接失败",
            Body = new MessageBody(
                "无法连接到数据库服务器。",
                InfoBarSeverity.Error,
                "请检查网络，或联系管理员确认连接串是否正确。"),
            CloseButtonText = "知道了",
            PreferredHeight = 240
        };

        var result = await _windows.ShowModalAsync(window);

        LastResult = $"消息窗口：{Describe(result)}";
    }

    /// <summary>确认窗口。主按钮给出明确动作，关闭原因分得清「取消」和「直接关掉」。</summary>
    [RelayCommand]
    private async Task ShowConfirmWindowAsync()
    {
        var window = new DialogWindow
        {
            Title = "删除租户",
            Body = new MessageBody(
                "将删除该租户下的全部数据，包括 1.2 万条业务记录。此操作不可恢复。",
                InfoBarSeverity.Warning),
            PrimaryButtonText = "永久删除",
            CloseButtonText = "取消",
            PreferredHeight = 250
        };

        var result = await _windows.ShowModalAsync(window);

        if (result != DialogResult.Primary)
        {
            LastResult = $"确认窗口：{Describe(result)}";
            return;
        }

        LastResult = "确认窗口：点了「永久删除」（演示而已，什么也没删）";
        _notify.Warning("演示而已，什么也没删。");
    }

    /// <summary>表单窗口：校验挂在窗口的主按钮上，不通过就把窗口留住。</summary>
    [RelayCommand]
    private async Task ShowFormWindowAsync()
    {
        var body = new TicketFormBody(PriorityOptions, ScopeOptions);

        var window = new DialogWindow
        {
            Title = "提交工单",
            Description = "表单比确认框长得多 —— 这正是该用真窗口而不是 ContentDialog 的场景。",
            Body = body,
            PrimaryButtonText = "提交",
            CloseButtonText = "取消",
            PreferredWidth = 480,
            PreferredHeight = 520
        };

        // 校验不通过时 args.Cancel 把窗口留住：用户刚填的东西一个字都不会丢。
        window.PrimaryButtonClick += (_, args) => args.Cancel = !body.Validate();

        var result = await _windows.ShowModalAsync(window);

        if (result != DialogResult.Primary)
        {
            LastResult = $"表单窗口：{Describe(result)}";
            return;
        }

        LastResult = $"表单窗口：已提交「{body.TicketTitle}」（{body.Scope} / {body.Priority}）";
        _notify.Success("工单已提交。");
    }

    /// <summary>嵌套模态窗口：在外层之上再叠一个，锁住的层级跟着往上挪。</summary>
    [RelayCommand]
    private async Task ShowNestedWindowAsync()
    {
        // 内层开过几次、分别是什么结果，攒在这儿最后一起汇报。
        var trail = new List<string>();

        var outer = new DialogWindow
        {
            Title = "外层模态窗口",
            Description = "点主按钮会在它之上再叠一个模态窗口 —— 那时连这一层也点不动了，只有最上面那个能操作。",
            Body = new MessageBody(
                "这是外层。它自己就是个模态窗口，但还能再叠一个上去。",
                InfoBarSeverity.Informational,
                "每叠一层，被锁住的就多一个窗口；关掉一层，只解开它下面那一层。"),
            PrimaryButtonText = "在它之上再开一个",
            CloseButtonText = "关闭",
            PreferredWidth = 480,
            PreferredHeight = 300
        };

        outer.PrimaryButtonClick += async (_, args) =>
        {
            // 先拦下外层自己的关闭 —— 否则点一下就关了，根本叠不起来。
            args.Cancel = true;

            try
            {
                trail.Add($"内层{Describe(await ShowInnerWindowAsync())}");
            }
            catch (Exception ex)
            {
                // 事件处理器是 async void，异常没有地方可以抛，只能就地兜住。
                _notify.Error($"打开内层窗口失败：{ex.Message}");
            }
        };

        var result = await _windows.ShowModalAsync(outer);

        LastResult = trail.Count == 0
            ? $"嵌套窗口：外层{Describe(result)}，没叠过内层"
            : $"嵌套窗口：{string.Join("、", trail)}，外层{Describe(result)}";
    }

    // ---- 非模态窗口 ----

    /// <summary>打开一个非模态窗口。不阻塞主窗口，而且可以同时开好几个。</summary>
    [RelayCommand]
    private void OpenModelessWindow()
    {
        var body = new HeartbeatBody();

        var window = new DialogWindow
        {
            Title = $"实时心跳 #{++_modelessSequence}",
            Description = "非模态窗口：开着它的同时，主窗口该怎么用还怎么用。",
            Body = body,
            CloseButtonText = "关闭窗口",
            PreferredWidth = 520,
            PreferredHeight = 360
        };

        // 非模态窗口没有人 await，不会有「关掉之后该做的事」，
        // 所以这类收尾只能自己挂在 Closed 上 —— 定时器不停，它会跟着进程一直跑。
        window.Closed += (_, _) => body.Stop();

        _windows.ShowModeless(window);
    }

    [RelayCommand(CanExecute = nameof(HasModelessWindow))]
    private void CloseAllModeless() => _windows.CloseAllModeless();

    // ---------------------------------------------------------------

    private bool HasModelessWindow() => ModelessCount > 0;

    private async Task<DialogResult> ShowInnerWindowAsync()
    {
        var inner = new DialogWindow
        {
            Title = "内层模态窗口",
            Body = new MessageBody(
                "这层是叠在外层之上的。",
                InfoBarSeverity.Success,
                "关掉它，外层才会重新可点；主窗口还是锁着的。"),
            CloseButtonText = "关闭",
            PreferredHeight = 230
        };

        var result = await _windows.ShowModalAsync(inner);

        LastResult = $"嵌套窗口：内层{Describe(result)}";
        return result;
    }

    private void OnModelessCountChanged(object? sender, EventArgs e)
        => ModelessCount = _windows.ModelessWindowCount;

    /// <summary>把关闭原因说成人话。</summary>
    private static string Describe(DialogResult result) => result switch
    {
        DialogResult.Primary => "点了主按钮",
        DialogResult.Secondary => "点了次要按钮",
        DialogResult.Close => "点了取消 / 关闭",
        _ => "直接关了标题栏的 X（没有结果）"
    };
}
