using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LwWinApp.Utils;

/// <summary>基于 System.Text.Json 的序列化封装，默认参数已按中文场景调好。</summary>
public static class JsonHelper
{
    /// <summary>默认选项：缩进输出、属性名保持原样、中文不转成 \uXXXX。</summary>
    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>紧凑输出，不做缩进，用于存库或网络传输。</summary>
    public static JsonSerializerOptions CompactOptions { get; } = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>序列化。失败返回 null 而不是抛异常。</summary>
    public static string? Serialize<T>(T value, bool indented = true)
    {
        try
        {
            return JsonSerializer.Serialize(value, indented ? DefaultOptions : CompactOptions);
        }
        catch (NotSupportedException)
        {
            // 类型里带了不支持序列化的成员（如 IntPtr、循环引用）。
            return null;
        }
    }

    /// <summary>反序列化。失败返回 default 而不是抛异常。</summary>
    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, DefaultOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>反序列化，并通过返回值告知是否成功（调用方需要区分「解析失败」和「值本来就是 null」时用）。</summary>
    public static bool TryDeserialize<T>(string? json, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            value = JsonSerializer.Deserialize<T>(json, DefaultOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>把 JSON 重新格式化成易读的缩进形式，用于展示。输入非法时原样返回。</summary>
    public static string Pretty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, DefaultOptions);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    /// <summary>判断字符串是不是合法 JSON。</summary>
    public static bool IsValid(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>深拷贝：序列化再反序列化。适合结构简单的 DTO。</summary>
    public static T? Clone<T>(T value)
        => Deserialize<T>(Serialize(value, indented: false));
}
