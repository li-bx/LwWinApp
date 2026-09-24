using LwWinApp.Core;
using LwWinApp.Data;
using LwWinApp.Utils;
using Microsoft.Extensions.Options;

namespace LwWinApp.Services;

/// <summary>
/// 用户可改的应用设置。
///
/// 分两类，边界要清楚：
/// <list type="bullet">
/// <item>只读的：来自 appsettings.json（连接串、默认分页等）。改它要动配置文件，界面上只展示。</item>
/// <item>可改的：存在 <see cref="LocalSettings"/> 里（导出目录、是否记住窗口位置）。
/// 用户改一次就生效，不需要管理员去改配置文件。</item>
/// </list>
/// 把这两类混在一起是设置页最容易变乱的地方，所以这里显式分开。
/// </summary>
public interface IAppSettingsService
{
    /// <summary>实际生效的导出目录。</summary>
    string ExportDirectory { get; }

    /// <summary>用户设置的导出目录。空字符串表示用默认目录。</summary>
    string ExportDirectoryOverride { get; set; }

    /// <summary>默认导出目录（我的文档\LwWinApp）。</summary>
    string DefaultExportDirectory { get; }

    /// <summary>退出时是否记住窗口尺寸与位置。</summary>
    bool RememberWindowState { get; set; }

    /// <summary>本地设置的存放路径。</summary>
    string SettingsFilePath { get; }

    /// <summary>日志目录。</summary>
    string LogDirectory { get; }

    /// <summary>目标数据库（不含密码）。</summary>
    DatabaseTarget DatabaseTarget { get; }

    /// <summary>配置里读到的默认每页条数，设置页展示用。</summary>
    int DefaultPageSize { get; }

    /// <summary>数据库连通性测试。</summary>
    Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AppSettingsService : IAppSettingsService
{
    private const string ExportDirectoryKey = "Export.Directory";
    private const string RememberWindowStateKey = "Window.RememberState";

    private readonly AppOptions _options;
    private readonly IDatabaseFactory _db;

    public AppSettingsService(IOptions<AppOptions> options, IDatabaseFactory db)
    {
        _options = options.Value;
        _db = db;
    }

    public string DefaultExportDirectory => FileHelper.DefaultExportDirectory;

    public string ExportDirectoryOverride
    {
        get => LocalSettings.GetString(ExportDirectoryKey, _options.ExportDirectory);
        set
        {
            LocalSettings.SetString(ExportDirectoryKey, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
            FileHelper.ExportDirectoryOverride = value;
        }
    }

    public string ExportDirectory => FileHelper.DefaultExportDirectory;

    public bool RememberWindowState
    {
        get => LocalSettings.GetBool(RememberWindowStateKey, _options.RememberWindowState);
        set => LocalSettings.SetBool(RememberWindowStateKey, value);
    }

    public string SettingsFilePath => LocalSettings.SettingsFilePath;

    public string LogDirectory => Path.Combine(LocalSettings.StorageDirectory, "logs");

    public DatabaseTarget DatabaseTarget => _db.DescribeTarget();

    public int DefaultPageSize => _options.PageSize;

    public Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken ct = default)
        => _db.TestConnectionAsync(ct);
}
