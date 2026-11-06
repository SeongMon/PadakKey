using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace PadakKey;
internal sealed class Mascot : IDisposable
{
    private readonly NotifyIcon tray;
    private readonly Settings settings;
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly List<(Icon Icon, int Delay)> frames = [];
    private int index;
    public static string Folder => Path.Combine(Storage.Root, "Icons");
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    public Mascot(NotifyIcon tray, Settings settings) { this.tray = tray; this.settings = settings; timer.Tick += (_, _) => { if (!settings.Animation || frames.Count < 2) return; index = (index + 1) % frames.Count; tray.Icon = frames[index].Icon; timer.Interval = frames[index].Delay; }; Reload(); }
    public void Reload()
    {
        timer.Stop(); tray.Icon = SystemIcons.Shield;
        foreach (var frame in frames) frame.Icon.Dispose(); frames.Clear(); index = 0;
        try
        {
            if (settings.Icon is "flutter" or "doze")
            {
                var sequence = settings.Icon == "flutter"
                    ? new[] { ("SparrowIdle", 3000), ("SparrowFlap", 140), ("SparrowIdle", 140), ("SparrowFlap", 140), ("SparrowIdle", 140), ("SparrowFlap", 140) }
                    : new[] { ("SparrowIdle", 3000), ("SparrowClosed", 500), ("SparrowNod", 1000), ("SparrowClosed", 400), ("SparrowNod", 800), ("SparrowClosed", 300) };
                foreach (var (name, delay) in sequence) { using var image = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", name + ".png")); frames.Add((Render(image, true, settings.IconSize, settings.IconZoom), delay)); }
            }
            else
            {
                if (Path.GetFileName(settings.Icon) != settings.Icon) throw new FormatException("아이콘 경로가 올바르지 않습니다.");
                frames.AddRange(ReadCustom(Path.Combine(Folder, settings.Icon), settings.IconSize, settings.IconZoom));
            }
            tray.Icon = frames[0].Icon;
            if (settings.Animation && frames.Count > 1) { timer.Interval = frames[0].Delay; timer.Start(); }
        }
        catch
        {
            foreach (var frame in frames) frame.Icon.Dispose(); frames.Clear();
            if (settings.Icon != "flutter") { settings.Icon = "flutter"; Reload(); }
        }
    }
    internal static List<(Icon Icon, int Delay)> ReadCustom(string file, int size = 100, int zoom = 100)
    {
        if (!new[] { ".png", ".gif" }.Contains(Path.GetExtension(file).ToLowerInvariant()) || new FileInfo(file).Length > 10 * 1024 * 1024) throw new FormatException("10MB 이하의 PNG 또는 GIF를 선택하세요.");
        using var image = Image.FromFile(file);
        int count = image.FrameDimensionsList.Contains(FrameDimension.Time.Guid) ? image.GetFrameCount(FrameDimension.Time) : 1;
        if (image.Width > 4096 || image.Height > 4096 || count > 120 || (long)image.Width * image.Height * count > 32_000_000) throw new FormatException("아이콘이 너무 큽니다. 최대 4096px, 120프레임, 합계 3,200만 픽셀입니다.");
        byte[]? delays = image.PropertyIdList.Contains(0x5100) ? image.GetPropertyItem(0x5100)?.Value : null;
        var result = new List<(Icon,int)>();
        try
        {
            for (int i = 0; i < count; i++)
            {
                if (count > 1) image.SelectActiveFrame(FrameDimension.Time, i);
                int delay = delays != null && delays.Length >= (i + 1) * 4 ? (int)Math.Clamp((long)BitConverter.ToInt32(delays, i * 4) * 10, 60, 10000) : 100;
                result.Add((Render(image, false, size, zoom), delay));
            }
            return result;
        }
        catch { foreach (var (icon, _) in result) icon.Dispose(); throw; }
    }
    private static Icon Render(Image image, bool builtin, int size, int zoom)
    {
        using var bitmap = new Bitmap(32, 32); using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor; graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var source = builtin ? new RectangleF(image.Width * .13f, image.Height * .13f, image.Width * .75f, image.Height * .75f) : new RectangleF(0, 0, image.Width, image.Height);
        float scale = Math.Min(32 / source.Width, 32 / source.Height) * Math.Clamp(size, 60, 100) / 100f * Math.Clamp(zoom, 100, 200) / 100f;
        graphics.DrawImage(image, new RectangleF((32 - source.Width * scale) / 2, (32 - source.Height * scale) / 2, source.Width * scale, source.Height * scale), source, GraphicsUnit.Pixel);
        IntPtr handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); } finally { DestroyIcon(handle); }
    }
    public void Dispose() { timer.Dispose(); tray.Icon = null; foreach (var frame in frames) frame.Icon.Dispose(); }
}
