using LwWinApp.Utils;

namespace LwWinApp.Views.Demo.Bodies;

/// <summary>
/// 非模态窗口里的「心跳」正文：每秒走一格，附一个暂停按钮。
///
/// 它存在的意义就是被看见 —— 窗口开着的时候回主窗口随便点，这个数字照样在涨，
/// 「非模态不阻塞属主」这件事光靠说很难让人信服。
///
/// 定时器归正文自己管：窗口关掉之后必须 <see cref="Stop"/>，
/// 否则它还会继续往一个已经没人看的控件上写字（而且这个窗口对象也回收不掉）。
/// </summary>
public sealed partial class HeartbeatBody : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private int _seconds;

    public HeartbeatBody()
    {
        InitializeComponent();

        _timer.Tick += OnTick;
        _timer.Start();

        UpdateTexts();
    }

    /// <summary>停掉计时。窗口关闭时必须调，调用方见 WindowDemoViewModel。</summary>
    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, object e)
    {
        _seconds++;
        UpdateTexts();
    }

    private void OnToggleClick(object sender, RoutedEventArgs e)
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            ToggleIcon.Glyph = "";   // Play
            ToggleText.Text = "继续";
            BeatText.Text = $"已暂停（累计 {_seconds} 秒）";
            return;
        }

        _timer.Start();
        ToggleIcon.Glyph = "";   // Pause
        ToggleText.Text = "暂停";
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        ElapsedText.Text = $"{_seconds / 60:00}:{_seconds % 60:00}";
        BeatText.Text = $"最近一次心跳：{DateTime.Now.ToString(DateTimeHelper.FormatTime)}";
    }
}
