using System.Security.Cryptography;
using System.Text.Json;
using PadakKey.Core;
namespace PadakKey;
internal sealed class Storage(string root)
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PadakKey");
    private string FilePath => Path.Combine(root, "accounts.dpapi");
    public List<Account> Load()
    {
        if (!File.Exists(FilePath)) return [];
        var plain = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
        try
        {
            var items = JsonSerializer.Deserialize<List<Account>>(plain) ?? throw new FormatException("계정 파일이 비어 있습니다.");
            foreach (var item in items) item.Validate();
            return items;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public void Save(List<Account> accounts)
    {
        foreach (var a in accounts) a.Validate();
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(accounts);
        try
        {
            byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(root);
            string temp = FilePath + "." + Guid.NewGuid() + ".tmp";
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(encrypted); file.Flush(true); }
                File.Move(temp, FilePath, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
internal sealed class Settings
{
    public bool Animation { get; set; } = true;
    public int IconZoom { get; set; } = 100;
    public int IconSize { get; set; } = 100;
    public string Icon { get; set; } = "flutter";
    private static string FilePath => Path.Combine(Storage.Root, "settings.json");
    public static Settings Load() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); } }
    public void Save() { Directory.CreateDirectory(Storage.Root); File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); }
}
