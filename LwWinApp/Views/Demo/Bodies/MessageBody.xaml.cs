using Microsoft.UI.Xaml.Media;

namespace LwWinApp.Views.Demo.Bodies;

/// <summary>
/// 「图标 + 一句话」的弹窗正文，消息型和确认型窗口都用它。
///
/// 图标和颜色按 <see cref="InfoBarSeverity"/> 走，和 DialogService 里那套消息对话框
/// （<c>ShowInfoAsync</c> / <c>ShowSuccessAsync</c> …）是同一份配色，
/// 换成窗口显示时视觉上不会变成另一种东西。
/// </summary>
public sealed partial class MessageBody : UserControl
{
    public MessageBody(string message, InfoBarSeverity severity = InfoBarSeverity.Informational, string? detail = null)
    {
        InitializeComponent();

        // 字形和画刷跟 DialogService.BuildContent 里那张表保持一致（E73E / E7BA / EA39 / E946），
        // 改这里记得两边一起改，否则同一个「成功」在窗口里和对话框里会长得不一样。
        var (glyph, brushKey) = severity switch
        {
            InfoBarSeverity.Success => ("", "SystemFillColorSuccessBrush"),
            InfoBarSeverity.Warning => ("", "SystemFillColorCautionBrush"),
            InfoBarSeverity.Error => ("", "SystemFillColorCriticalBrush"),
            _ => ("", "SystemFillColorAttentionBrush")
        };

        Icon.Glyph = glyph;

        // 取不到画刷就用默认色，别为了一个图标把窗口搞崩。
        if (Application.Current.Resources.TryGetValue(brushKey, out var brush) && brush is Brush fill)
            Icon.Foreground = fill;

        MessageText.Text = message;

        DetailText.Text = detail ?? string.Empty;
        DetailText.Visibility = string.IsNullOrWhiteSpace(detail)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
