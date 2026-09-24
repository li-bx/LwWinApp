using System.Security.Cryptography;
using System.Text;

namespace LwWinApp.Utils;

/// <summary>常用加解密工具。</summary>
public static class EncryptHelper
{
    // ---------------- 哈希 ----------------

    /// <summary>MD5，返回小写十六进制。仅用于校验和，不要用于密码。</summary>
    public static string Md5(string input)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    /// <summary>SHA256，返回小写十六进制。</summary>
    public static string Sha256(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    /// <summary>SHA512，返回小写十六进制。</summary>
    public static string Sha512(string input)
        => Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    /// <summary>计算文件的 SHA256，用于校验文件是否被改动。</summary>
    public static async Task<string> FileSha256Async(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ---------------- Base64 ----------------

    public static string ToBase64(string input)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(input));

    public static string FromBase64(string base64)
        => Encoding.UTF8.GetString(Convert.FromBase64String(base64));

    /// <summary>URL 安全的 Base64（把 +/ 换成 -_ 并去掉补齐的 =）。</summary>
    public static string ToBase64Url(string input)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(input))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static bool TryFromBase64(string base64, out string result)
    {
        try
        {
            result = FromBase64(base64);
            return true;
        }
        catch (FormatException)
        {
            result = string.Empty;
            return false;
        }
    }

    // ---------------- AES ----------------

    private const int AesKeySize = 32;  // 256 位
    private const int AesNonceSize = 12; // GCM 推荐的 IV 长度
    private const int AesTagSize = 16;

    /// <summary>
    /// AES-GCM 加密。输出格式：Base64(随机IV + 认证标签 + 密文)。
    /// 用 GCM 而不是 CBC，是因为它自带完整性校验，能发现密文被篡改。
    /// </summary>
    public static string AesEncrypt(string plainText, string key)
    {
        var keyBytes = DeriveKey(key);
        var nonce = RandomNumberGenerator.GetBytes(AesNonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[AesTagSize];

        using var aes = new AesGcm(keyBytes, AesTagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var payload = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length + tag.Length, cipherBytes.Length);

        return Convert.ToBase64String(payload);
    }

    /// <summary>AES-GCM 解密。密钥不对或密文被改过都会抛 CryptographicException。</summary>
    public static string AesDecrypt(string cipherText, string key)
    {
        var keyBytes = DeriveKey(key);
        var payload = Convert.FromBase64String(cipherText);

        if (payload.Length < AesNonceSize + AesTagSize)
            throw new CryptographicException("密文长度不足，数据可能已损坏。");

        var nonce = payload.AsSpan(0, AesNonceSize);
        var tag = payload.AsSpan(AesNonceSize, AesTagSize);
        var cipherBytes = payload.AsSpan(AesNonceSize + AesTagSize);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(keyBytes, AesTagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    public static bool TryAesDecrypt(string cipherText, string key, out string result)
    {
        try
        {
            result = AesDecrypt(cipherText, key);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            result = string.Empty;
            return false;
        }
    }

    /// <summary>把任意长度的口令拉伸成固定 32 字节密钥。同一个口令永远得到同一个密钥。</summary>
    private static byte[] DeriveKey(string key)
        => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    // ---------------- 随机串 ----------------

    /// <summary>生成指定长度的随机字符串（大小写字母 + 数字），用于盐、令牌等。</summary>
    public static string RandomString(int length, string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
    {
        if (length <= 0) return string.Empty;

        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

        return new string(chars);
    }

    /// <summary>生成密码学安全的随机字节，返回 Base64。</summary>
    public static string RandomToken(int byteLength = 32)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
