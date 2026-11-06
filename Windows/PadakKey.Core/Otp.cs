using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PadakKey.Core;
public sealed record Account(Guid Id, string Issuer, string Name, string Secret, string Algorithm = "SHA1", int Digits = 6, int Period = 30)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 256 || Issuer.Length > 256) throw new FormatException("계정 이름을 확인하세요.");
        if (Digits is not (6 or 8) || Period < 1 || Period > 300 || Algorithm is not ("SHA1" or "SHA256" or "SHA512")) throw new FormatException("지원하지 않는 OTP 설정입니다.");
        var key = Otp.Decode(Secret); CryptographicOperations.ZeroMemory(key);
    }
    public string Code(long seconds)
    {
        Validate();
        if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var key = Otp.Decode(Secret);
        try
        {
            Span<byte> counter = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(counter, seconds / Period);
            var hash = Algorithm switch { "SHA256" => HMACSHA256.HashData(key, counter), "SHA512" => HMACSHA512.HashData(key, counter), _ => HMACSHA1.HashData(key, counter) };
            int offset = hash[^1] & 15;
            int value = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7fffffff;
            CryptographicOperations.ZeroMemory(hash);
            return (value % (Digits == 6 ? 1_000_000 : 100_000_000)).ToString("D" + Digits);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
public static class Otp
{
    public static byte[] Decode(string value)
    {
        string text = value.Replace(" ", "").Replace("-", "").ToUpperInvariant().TrimEnd('=');
        if (text.Length < 2 || text.Length > 2048 || text.Length % 8 is 1 or 3 or 6) throw new FormatException("올바른 Base32 인증 키를 입력하세요.");
        var result = new List<byte>(); int bits = 0, buffer = 0;
        foreach (char c in text)
        {
            int n = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".IndexOf(c);
            if (n < 0) throw new FormatException("올바른 Base32 인증 키를 입력하세요.");
            buffer = (buffer << 5) | n; bits += 5;
            if (bits >= 8) { bits -= 8; result.Add((byte)(buffer >> bits)); buffer &= (1 << bits) - 1; }
        }
        if (buffer != 0 || result.Count == 0) throw new FormatException("인증 키의 길이 또는 패딩이 올바르지 않습니다.");
        return result.ToArray();
    }
    public static Account Parse(string value)
    {
        if (value.Length > 8192 || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "otpauth" || uri.Host != "totp") throw new FormatException("otpauth://totp QR 코드만 지원합니다. HOTP와 일괄 이전 QR은 지원하지 않습니다.");
        var query = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = pair.Split('=', 2);
            string k = Uri.UnescapeDataString(p[0]);
            if (!query.TryAdd(k, p.Length == 2 ? Uri.UnescapeDataString(p[1].Replace("+", " ")) : "")) throw new FormatException("중복된 OTP 설정입니다.");
        }
        string label = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var split = label.Split(':', 2); string issuer = query.GetValueOrDefault("issuer", split.Length == 2 ? split[0] : "");
        if (split.Length == 2 && query.ContainsKey("issuer") && split[0] != issuer) throw new FormatException("발급자 설정이 일치하지 않습니다.");
        var a = new Account(Guid.NewGuid(), issuer, split[^1], query.GetValueOrDefault("secret", ""), query.GetValueOrDefault("algorithm", "SHA1").ToUpperInvariant(), int.Parse(query.GetValueOrDefault("digits", "6")), int.Parse(query.GetValueOrDefault("period", "30")));
        a.Validate(); return a;
    }
}
