using System.Security.Cryptography;
using System.Text;

namespace LwWinApp.Utils;

/// <summary>
/// 密码哈希与校验。
///
/// 支持两种存储格式，<see cref="Verify"/> 会自动识别：
///   1. <c>PBKDF2$迭代次数$Base64盐$Base64哈希</c> —— 新建/改密码时写入的格式，每用户独立随机盐。
///   2. 纯十六进制 SHA256 —— 早期格式（演示种子数据用的就是它），校验通过后可提示升级。
///
/// 之所以保留第二种，是因为数据库里的演示账号是 SQL 脚本用 <c>SHA2()</c> 生成的，
/// 应用必须能登录进去。等用户改了密码就会自动换成第一种。
/// </summary>
public static class PasswordHasher
{
    private const string Pbkdf2Prefix = "PBKDF2";
    private const int Pbkdf2Iterations = 210_000; // OWASP 2023 对 PBKDF2-SHA256 的建议值
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>
    /// 旧格式（SHA256(明文 + 盐)）使用的盐。
    /// 必须与 Sql/init.sql 里 <c>SHA2(CONCAT('123456', 'LwWinApp@2026'), 256)</c> 一致。
    /// 由 App 启动时从 appsettings.json 的 App:PasswordSalt 注入。
    /// </summary>
    public static string LegacySalt { get; set; } = "LwWinApp@2026";

    /// <summary>计算新格式哈希（PBKDF2 + 随机盐）。</summary>
    public static string Hash(string plainPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainPassword);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainPassword), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Pbkdf2Prefix}${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>计算旧格式哈希，与种子数据保持一致。</summary>
    public static string HashLegacy(string plainPassword)
        => Convert.ToHexString(
               SHA256.HashData(Encoding.UTF8.GetBytes(plainPassword + LegacySalt)))
           .ToLowerInvariant();

    /// <summary>校验密码。存储值为空或格式不认识时一律返回 false。</summary>
    public static bool Verify(string plainPassword, string? storedHash)
    {
        if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        return storedHash.StartsWith(Pbkdf2Prefix + "$", StringComparison.Ordinal)
            ? VerifyPbkdf2(plainPassword, storedHash)
            : VerifyLegacy(plainPassword, storedHash);
    }

    /// <summary>存储值是否需要换成新格式（旧格式或迭代次数偏低时要升级）。</summary>
    public static bool NeedsUpgrade(string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return true;
        if (!storedHash.StartsWith(Pbkdf2Prefix + "$", StringComparison.Ordinal)) return true;

        var parts = storedHash.Split('$');
        return parts.Length < 4
            || !int.TryParse(parts[1], out var iterations)
            || iterations < Pbkdf2Iterations;
    }

    private static bool VerifyPbkdf2(string plainPassword, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4) return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainPassword), salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        // 定长时间比较，避免通过响应时间差推断出哈希内容。
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool VerifyLegacy(string plainPassword, string storedHash)
    {
        var expected = Encoding.UTF8.GetBytes(HashLegacy(plainPassword));
        var actual = Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant());

        // 长度不等时 FixedTimeEquals 会直接返回 false，这里先挡掉。
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
