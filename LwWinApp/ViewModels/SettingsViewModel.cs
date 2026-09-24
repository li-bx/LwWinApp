using System.Runtime.InteropServices;
using LwWinApp.Core;
using LwWinApp.Models.Dtos;
using LwWinApp.Services;
using LwWinApp.Utils;

namespace LwWinApp.ViewModels;

/// <summary>
/// 系统设置。
///
/// 界面上把「可改的」和「只读的」分开摆：主题、导出目录、窗口记忆能改；
/// 连接串来自 appsettings.json，只展示并提供连通性测试。
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IThemeService _theme;
    private readonly IAppSettingsService _settings;
    private readonly IFilePickerService _filePicker;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notify;

    public SettingsViewModel(
        IThemeService theme,
        IAppSettingsService settings,
        IFilePickerService filePicker,
        IDialogService dialogs,
        INotificationService notify)
    {
        _theme = theme;
        _settings = settings;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _notify = notify;

        Title = "系统设置";

        ThemeOptions =
        [
            new SelectOption("跟随系统", ThemeMode.Default),
            new SelectOption("浅色", ThemeMode.Light),
            new SelectOption("深色", ThemeMode.Dark)
        ];

        _selectedTheme = ThemeOptions.FirstOrDefault(o => o.AsEnum<ThemeMode>() == _theme.Current) ?? ThemeOptions[0];

        _exportDirectory = _settings.ExportDirectoryOverride;
        _rememberWindowState = _settings.RememberWindowState;

        // 用具名方法而不是 lambda：标签关闭时要能退订。
        // 用 lambda 的话这个 VM 会被 ThemeService 永久持有，关掉的标签也回收不了。
        _theme.ThemeChanged += OnThemeChanged;
    }

    public override void OnTabClosed() => _theme.ThemeChanged -= OnThemeChanged;

    private void OnThemeChanged(object? sender, ThemeMode mode)
        => DispatcherHelper.RunAsync(SyncThemeSelection);

    // ---- 外观 ----

    public IReadOnlyList<SelectOption> ThemeOptions { get; }

    [ObservableProperty]
    private SelectOption? _selectedTheme;

    partial void OnSelectedThemeChanged(SelectOption? value)
    {
        if (value.AsEnum<ThemeMode>() is { } mode)
            _theme.Apply(mode);
    }

    // ---- 文件 ----

    [ObservableProperty]
    private string _exportDirectory = string.Empty;

    public string EffectiveExportDirectory => _settings.ExportDirectory;

    public string SettingsFilePath => _settings.SettingsFilePath;

    public string LogDirectory => _settings.LogDirectory;

    [RelayCommand]
    private async Task ChooseExportDirectoryAsync()
    {
        var folder = await _filePicker.PickFolderAsync("选择此文件夹");

        if (folder is null) return;

        ExportDirectory = folder.Path;
        ApplyExportDirectory();
    }

    [RelayCommand]
    private void ApplyExportDirectory()
    {
        _settings.ExportDirectoryOverride = ExportDirectory;

        OnPropertyChanged(nameof(EffectiveExportDirectory));

        _notify.Success($"导出目录已设为：{_settings.ExportDirectory}");
    }

    [RelayCommand]
    private void ResetExportDirectory()
    {
        _settings.ExportDirectoryOverride = string.Empty;
        ExportDirectory = string.Empty;

        OnPropertyChanged(nameof(EffectiveExportDirectory));

        _notify.Info($"已恢复默认导出目录：{_settings.ExportDirectory}");
    }

    [RelayCommand]
    private void OpenExportDirectory() => FileHelper.RevealInExplorer(_settings.ExportDirectory);

    [RelayCommand]
    private void OpenSettingsFolder() => FileHelper.RevealInExplorer(_settings.SettingsFilePath);

    [RelayCommand]
    private void OpenLogFolder() => FileHelper.RevealInExplorer(_settings.LogDirectory);

    // ---- 窗口 ----

    [ObservableProperty]
    private bool _rememberWindowState;

    partial void OnRememberWindowStateChanged(bool value) => _settings.RememberWindowState = value;

    // ---- 数据库 ----

    public string DatabaseServer => _settings.DatabaseTarget.Display;

    public string DatabaseUser => _settings.DatabaseTarget.User;

    /// <summary>配置里读到的默认每页条数。只读展示——列表页上随时能改。</summary>
    public int DefaultPageSize => _settings.DefaultPageSize;

    [ObservableProperty]
    private string _connectionMessage = "尚未测试连接。";

    [ObservableProperty]
    private InfoBarSeverity _connectionSeverity = InfoBarSeverity.Informational;

    [ObservableProperty]
    private bool _isConnectionMessageOpen;

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        // 返回的是值元组，不能走 RunBusyAsync<TResult>：
        // 那个重载返回 TResult?，而泛型上的 T? 对值类型不产生 Nullable<T>，
        // 「拿不到结果」和「结果为 false」就分不清了。用 TryRunBusyAsync 更直白。
        var result = default((bool Ok, string Message));

        var done = await TryRunBusyAsync(
            async () => result = await _settings.TestConnectionAsync(),
            "连接测试失败");

        if (!done) return;

        ConnectionMessage = result.Ok
            ? $"连接正常：{result.Message}"
            : $"连接失败：{result.Message}";

        ConnectionSeverity = result.Ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        IsConnectionMessageOpen = true;

        if (result.Ok)
            _notify.Success("数据库连接正常。");
    }

    // ---- 关于本机 ----

    public string AppName => AppConstants.AppName;

    public string AppVersion => AppConstants.AppVersion;

    public string RuntimeInfo => RuntimeInformation.FrameworkDescription;

    public string OperatingSystem => RuntimeInformation.OSDescription;

    public string ProcessArchitecture => RuntimeInformation.ProcessArchitecture.ToString();

    public override Task OnNavigatedToAsync(object? parameter)
    {
        SyncThemeSelection();
        OnPropertyChanged(nameof(EffectiveExportDirectory));

        return Task.CompletedTask;
    }

    /// <summary>主题也可能从标题栏那个快捷按钮改，改完把下拉框选项同步回来。</summary>
    private void SyncThemeSelection()
    {
        var match = ThemeOptions.FirstOrDefault(o => o.AsEnum<ThemeMode>() == _theme.Current);

        if (match is not null && !ReferenceEquals(match, SelectedTheme))
            SelectedTheme = match;
    }
}
