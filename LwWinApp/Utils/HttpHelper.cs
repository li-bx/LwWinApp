using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LwWinApp.Utils;

/// <summary>HTTP 调用结果。</summary>
public sealed record HttpResult<T>(bool Ok, int StatusCode, T? Data, string? Error)
{
    public static HttpResult<T> Success(int statusCode, T? data) => new(true, statusCode, data, null);
    public static HttpResult<T> Failure(int statusCode, string error) => new(false, statusCode, default, error);
}

/// <summary>
/// 轻量 HTTP 客户端封装。
/// 复用同一个 HttpClient 实例（每个实例都会开自己的连接池，反复 new 会耗尽端口）。
/// </summary>
public static class HttpHelper
{
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>全局超时时间。</summary>
    public static TimeSpan Timeout
    {
        get => Client.Timeout;
        set => Client.Timeout = value;
    }

    /// <summary>给所有请求挂一个固定的请求头（如 Authorization）。</summary>
    public static void SetDefaultHeader(string name, string? value)
    {
        Client.DefaultRequestHeaders.Remove(name);
        if (!string.IsNullOrEmpty(value))
            Client.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
    }

    public static Task<HttpResult<T>> GetJsonAsync<T>(string url, CancellationToken ct = default)
        => SendJsonAsync<T>(new HttpRequestMessage(HttpMethod.Get, url), ct);

    public static Task<HttpResult<T>> PostJsonAsync<T>(string url, object? body = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return SendJsonAsync<T>(request, ct);
    }

    public static Task<HttpResult<T>> PutJsonAsync<T>(string url, object? body = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url);
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return SendJsonAsync<T>(request, ct);
    }

    public static Task<HttpResult<T>> DeleteJsonAsync<T>(string url, CancellationToken ct = default)
        => SendJsonAsync<T>(new HttpRequestMessage(HttpMethod.Delete, url), ct);

    /// <summary>取纯文本（下载网页、读接口返回的字符串）。</summary>
    public static async Task<HttpResult<string>> GetStringAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var response = await Client.GetAsync(url, ct);
            var text = await response.Content.ReadAsStringAsync(ct);

            return response.IsSuccessStatusCode
                ? HttpResult<string>.Success((int)response.StatusCode, text)
                : HttpResult<string>.Failure((int)response.StatusCode, text.Truncate(200));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HttpResult<string>.Failure(0, Describe(ex));
        }
    }

    /// <summary>下载文件到本地，返回是否成功。</summary>
    public static async Task<(bool Ok, string Message)> DownloadFileAsync(
        string url, string savePath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return (false, $"服务器返回 {(int)response.StatusCode}");

            var total = response.Content.Headers.ContentLength ?? -1L;
            FileHelper.EnsureParentDirectory(savePath);

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(savePath);

            var buffer = new byte[81920];
            long read = 0;
            int count;

            while ((count = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, count), ct);
                read += count;

                if (total > 0)
                    progress?.Report(read * 100d / total);
            }

            progress?.Report(100);
            return (true, $"已保存到 {savePath}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return (false, Describe(ex));
        }
    }

    private static async Task<HttpResult<T>> SendJsonAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using (request)
            {
                using var response = await Client.SendAsync(request, ct);
                var status = (int)response.StatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct);
                    return HttpResult<T>.Failure(status, error.Truncate(300));
                }

                // 204 或空响应体时没有内容可反序列化。
                if (response.StatusCode == HttpStatusCode.NoContent ||
                    response.Content.Headers.ContentLength == 0)
                {
                    return HttpResult<T>.Success(status, default);
                }

                var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
                return HttpResult<T>.Success(status, data);
            }
        }
        catch (JsonException ex)
        {
            return HttpResult<T>.Failure(0, $"返回内容不是合法 JSON：{ex.Message}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HttpResult<T>.Failure(0, Describe(ex));
        }
    }

    private static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException => "请求超时或已取消",
        HttpRequestException http => $"网络请求失败：{http.Message}",
        _ => ex.Message
    };
}
