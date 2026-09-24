using LwWinApp.Core;
using LwWinApp.Data;
using LwWinApp.Data.Repositories;
using LwWinApp.Services;
using LwWinApp.Utils;
using LwWinApp.ViewModels;
using LwWinApp.ViewModels.Demo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.UI.Dispatching;

namespace LwWinApp;

/// <summary>
/// 应用入口。
///
/// 职责只有三件事：组装依赖注入容器、创建主窗口、把窗口相关的服务接上。
/// 界面逻辑一律不写在这里 —— 这是个容易被塞满的类，一塞满就再也拆不开了。
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    private IWindowContext? _windowContext;

    /// <summary>当前的 App 实例。等于 <c>Application.Current as App</c>，只是省一次转换。</summary>
    public static App? Instance => Current as App;

    /// <summary>服务容器。容器未建好时为 null。</summary>
    public static IServiceProvider? Services => Instance?._host.Services;

    /// <summary>取服务。取不到时返回 null，调用方自己决定怎么降级。</summary>
    public static T? GetService<T>() where T : class => Services?.GetService<T>();

    public App()
    {
        InitializeComponent();

        // DispatcherHelper 是所有跨线程回调的基础，必须最先初始化。
        DispatcherHelper.Initialize(DispatcherQueue.GetForCurrentThread());

        _host = BuildHost();
        StartHost();
        ApplyRuntimeConfiguration();

        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var window = new MainWindow();

            // 顺序有讲究：先建窗口，再把窗口交给服务。
            // ThemeService 要用 Content 根元素，WindowService 要用 AppWindow，
            // 两者都依赖 Attach 之后的 WindowContext。
            _windowContext = GetService<IWindowContext>();
            _windowContext?.Attach(window);

            GetService<IThemeService>()?.Initialize();

            var windowService = GetService<IWindowService>();
            windowService?.Initialize(window);

            var options = GetService<IOptions<AppOptions>>()?.Value;
            if (options?.RememberWindowState != false)
                windowService?.TryRestoreWindowState();

            window.Closed += OnWindowClosed;
            window.Activate();
        }
        catch (Exception ex)
        {
            LogFatal("主窗口创建失败", ex);
            throw;
        }
    }

    // ---------------------------------------------------------------
    // 容器
    // ---------------------------------------------------------------

    private static IHost BuildHost() => new HostBuilder()
        // WinUI 应用的工作目录是启动它的地方，不是 exe 所在目录。
        // 不显式指定内容根，appsettings.json 会找不到。
        .UseContentRoot(AppContext.BaseDirectory)
        .ConfigureAppConfiguration((_, config) =>
        {
            config.SetBasePath(AppContext.BaseDirectory);
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            // 允许用环境变量覆盖配置，方便在客户现场临时改连接串：
            //   set LWWINAPP_ConnectionStrings__LwWinApp=...
            config.AddEnvironmentVariables("LWWINAPP_");
        })
        .ConfigureLogging((context, logging) =>
        {
            logging.ClearProviders();
            logging.AddConfiguration(context.Configuration.GetSection("Logging"));
            logging.AddDebug();

            // 桌面应用没有控制台，文件日志是排查线上问题的唯一手段。
            logging.AddProvider(new FileLoggerProvider(LogDirectory, LogLevel.Information));
        })
        .ConfigureServices((context, services) => ConfigureServices(context.Configuration, services))
        .Build();

    private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        services.AddSingleton(configuration);
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));

        // ---- 数据层 ----
        services.AddSingleton<IDatabaseFactory, MySqlDatabaseFactory>();
        services.AddSingleton<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IRoleRepository, RoleRepository>();
        services.AddSingleton<IDeptRepository, DeptRepository>();
        services.AddSingleton<ILogRepository, LogRepository>();

        // ---- 服务层 ----
        // 全部单例：它们持有的是窗口引用或页面容器，本来就只有一份。
        services.AddSingleton<IWindowContext, WindowContext>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ITabNavigationService, TabNavigationService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();

        // ---- 业务服务 ----
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IRoleService, RoleService>();
        services.AddSingleton<IDeptService, DeptService>();
        services.AddSingleton<IDashboardService, DashboardService>();

        // ---- ViewModel ----
        // 全部 Transient：页面实例由 XAML 体系创建（只认无参构造函数），
        // ViewModel 由页面在构造函数里从容器取，所以一个标签一份、互不干扰。
        //
        // 外壳是个例外：它的状态要活到窗口关闭，用单例。
        services.AddSingleton<ShellViewModel>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<UserManageViewModel>();
        services.AddTransient<RoleManageViewModel>();
        services.AddTransient<DeptTreeViewModel>();
        services.AddTransient<OperationLogViewModel>();
        services.AddTransient<SettingsViewModel>();

        // ---- 示例页 ViewModel ----
        services.AddTransient<ControlsDemoViewModel>();
        services.AddTransient<DialogDemoViewModel>();
        services.AddTransient<ProgressDemoViewModel>();
        services.AddTransient<NotificationDemoViewModel>();
        services.AddTransient<FormValidationViewModel>();
        services.AddTransient<GroupedTableViewModel>();
    }

    /// <summary>
    /// 把配置里那些「不在容器里」的静态选项落到实处。
    /// PasswordHasher 和 FileHelper 是静态类，只能这样注入配置。
    /// </summary>
    private void ApplyRuntimeConfiguration()
    {
        var options = GetService<IOptions<AppOptions>>()?.Value;
        if (options is null) return;

        PasswordHasher.LegacySalt = options.PasswordSalt;

        // 导出目录以用户设置优先（设置页可能改过），没改过才用配置文件里的值。
        FileHelper.ExportDirectoryOverride =
            GetService<IAppSettingsService>()?.ExportDirectoryOverride ?? options.ExportDirectory;

        // 配置里的默认主题只在首次运行时生效；之后一切以本地设置为准，
        // 否则用户手动切过的主题每次启动都会被配置文件覆盖回去。
        if (LocalSettings.GetString(ThemeService.SettingKey) is null
            && Enum.TryParse<ThemeMode>(options.DefaultTheme, ignoreCase: true, out var theme))
        {
            LocalSettings.SetEnum(ThemeService.SettingKey, theme);
        }
    }

    private void StartHost()
    {
        try
        {
            _host.Start();
        }
        catch (Exception ex)
        {
            // 容器起不来不代表界面不能用：连接串缺失、日志目录没权限都属于这一类。
            // 真正的错误会在第一次用数据库时暴露给用户。
            LogFatal("依赖注入容器启动失败", ex);
        }
    }

    // ---------------------------------------------------------------
    // 生命周期
    // ---------------------------------------------------------------

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        try
        {
            // 不阻塞 UI 线程：StopAsync 在窗口关闭事件里同步等待会死锁。
            _ = _host.StopAsync(TimeSpan.FromSeconds(2)).ContinueWith(_ => _host.Dispose());
        }
        catch (Exception ex)
        {
            LogFatal("停止应用容器时出错", ex);
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogFatal($"未处理异常：{e.Message}", e.Exception);

        // 刻意不设 e.Handled = true。
        // 页面加载和命令执行都已经各自兜住异常了，能走到这里的都是没预料到的问题，
        // 压着不让它崩只会让状态变得更难收拾，不如留下日志后正常退出。
    }

    // ---------------------------------------------------------------

    private static string LogDirectory
        => Path.Combine(LocalSettings.StorageDirectory, "logs");

    private void LogFatal(string message, Exception? ex)
    {
        try
        {
            var logger = GetService<ILoggerFactory>()?.CreateLogger<App>();

            if (logger is not null)
            {
                logger.LogCritical(ex, "{Message}", message);
                return;
            }

            // 日志系统还没建好（比如 Host 构建阶段就炸了）时，直接落一个文件。
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(
                Path.Combine(LogDirectory, "startup-error.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}{ex}{Environment.NewLine}");
        }
        catch
        {
            // 连日志都写不了就没别的办法了，绝不能在这里再抛。
        }
    }
}
