using LwWinApp.Models.Dtos;

namespace LwWinApp.Views.Demo.Bodies;

/// <summary>
/// 弹窗窗口里的工单表单。
///
/// 表单自己负责「取值」和「校验」，外面（ViewModel）只读结果 ——
/// 这样窗口、正文、业务三件事各归各的，正文换成另一张表单也不用动窗口那一层。
/// </summary>
public sealed partial class TicketFormBody : UserControl
{
    public TicketFormBody(
        IReadOnlyList<SelectOption> priorityOptions,
        IReadOnlyList<SelectOption> scopeOptions)
    {
        InitializeComponent();

        PriorityBox.ItemsSource = priorityOptions;
        ScopeBox.ItemsSource = scopeOptions;

        // 下拉框先选第一项，省得用户以为可以空着不选。
        PriorityBox.SelectedIndex = 0;
        ScopeBox.SelectedIndex = 0;
    }

    public string TicketTitle => TitleBox.Text.Trim();

    public string Priority => SelectedText(PriorityBox);

    public string Scope => SelectedText(ScopeBox);

    public string? Detail
        => string.IsNullOrWhiteSpace(DetailBox.Text) ? null : DetailBox.Text.Trim();

    /// <summary>
    /// 校验一遍。不通过时把原因显示在表单顶部并返回 false。
    ///
    /// 调用方直接把它接到窗口的 <c>args.Cancel</c> 上即可：<c>args.Cancel = !body.Validate();</c>
    /// —— 窗口留住，用户刚填的东西一个字都不会丢。
    /// </summary>
    public bool Validate()
    {
        var error = string.IsNullOrWhiteSpace(TicketTitle)
            ? "工单标题不能为空。"
            : TicketTitle.Length > 60
                ? "工单标题不能超过 60 个字。"
                : null;

        ErrorBar.Message = error ?? string.Empty;
        ErrorBar.IsOpen = error is not null;

        return error is null;
    }

    private static string SelectedText(ComboBox box)
        => box.SelectedItem is SelectOption option ? option.Text : "—";
}
