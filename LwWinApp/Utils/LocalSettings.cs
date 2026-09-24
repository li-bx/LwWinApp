using System.Text.Json;

namespace LwWinApp.Utils;

/// <summary>
/// 本地设置持久化（主题、窗口状态等）。
///
/// 没有用 <c>ApplicationData.Current.LocalSettings</c>，因为它要求应用有包标识，
/// 我们的应用是无打包（unpackaged）运行的，访问它会直接抛异常。
/// 这里退到 %LOCALAPPDATA%\LwWinApp\settings.json，无打包/打包两种模式都能用。
///
/// 写入是「立即落盘」的，但失败只记录不抛——设置存不下来不该让应用崩掉。
/// </summary>
public static class LocalSettings
{
    private static readonly object SyncRoot = new();
    private static readonly string FilePath = BuildFilePath();
    private static Dictionary<string, string>? _cache;

    private static string BuildFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Core.AppConstants.AppName,
        "settings.json");

    /// <summary>读取字符串。键不存在返回 null。</summary>
    public static string? GetString(string key)
    {
        lock (SyncRoot)
        {
            var data = Load();
            return data.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <summary>读取字符串，不存在时返回默认值。</summary>
    public static string GetString(string key, string defaultValue)
        => GetString(key) ?? defaultValue;

    public static void SetString(string key, string? value)
    {
        lock (SyncRoot)
        {
            var data = Load();

            if (value is null)
            {
                if (!data.Remove(key)) return;
            }
            else
            {
                if (data.TryGetValue(key, out var existing) && existing == value) return;
                data[key] = value;
            }

            Save(data);
        }
    }

    /// <summary>读取布尔值。存的是 "true"/"false"，解析失败按默认值处理。</summary>
    public static bool GetBool(string key, bool defaultValue = false)
        => bool.TryParse(GetString(key), out var value) ? value : defaultValue;

    public static void SetBool(string key, bool value) => SetString(key, value ? "true" : "false");

    /// <summary>读取整数。解析失败按默认值处理。</summary>
    public static int GetInt(string key, int defaultValue = 0)
        => int.TryParse(GetString(key), out var value) ? value : defaultValue;

    public static void SetInt(string key, int value)
        => SetString(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>读取枚举。名字不认识时按默认值处理。</summary>
    public static TEnum GetEnum<TEnum>(string key, TEnum defaultValue) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(GetString(key), ignoreCase: true, out var value) ? value : defaultValue;

    public static void SetEnum<TEnum>(string key, TEnum value) where TEnum : struct, Enum
        => SetString(key, value.ToString());

    /// <summary>删除一个键。</summary>
    public static void Remove(string key) => SetString(key, null);

    /// <summary>清空全部设置。</summary>
    public static void Clear()
    {
        lock (SyncRoot)
        {
            _cache = [];
            Save(_cache);
        }
    }

    /// <summary>
    /// 应用数据目录 —— <c>%LOCALAPPDATA%\LwWinApp</c>。
    /// 日志目录、导出目录都挂在它下面，要「打开所在目录」也用这个。
    /// </summary>
    public static string StorageDirectory => Path.GetDirectoryName(FilePath)!;

    /// <summary>设置文件的完整路径 —— <c>…\LwWinApp\settings.json</c>。要「显示配置文件路径」用这个。</summary>
    public static string SettingsFilePath => FilePath;

    private static Dictionary<string, string> Load()
    {
        if (_cache is not null) return _cache;

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            }
            else
            {
                _cache = [];
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 文件损坏或读不了就当空的，不要因此启动失败。
            _cache = [];
        }

        return _cache;
    }

    private static void Save(Dictionary<string, string> data)
    {
        try
        {
            FileHelper.EnsureParentDirectory(FilePath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonHelper.DefaultOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 存不下来只影响下次启动恢复偏好，不影响本次使用。
        }
    }
}
