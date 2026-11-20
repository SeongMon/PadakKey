using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace PadakKey;
internal sealed class Mascot : IDisposable
{
    private const int Canvas = 32, RingInset = 4;
    public const int CountdownPeriod = 30;
    private readonly NotifyIcon tray;
    private readonly Settings settings;
    private readonly System.Windows.Forms.Timer timer = new();
    // Redraws the countdown ring once per second, aligned to the second boundary. Stopped when the ring is off or the session is locked.
    private readonly System.Windows.Forms.Timer countdown = new();
    private readonly List<(Bitmap Image, int Delay)> frames = [];
    private Icon? shown;
    private int index;
    private bool paused;
    private DateTime copiedUntil = DateTime.MinValue;
    public static string Folder => Path.Combine(Storage.Root, "Icons");
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    public Mascot(NotifyIcon tray, Settings settings)
    {
        this.tray = tray; this.settings = settings;
        timer.Tick += (_, _) => { if (!settings.Animation || frames.Count < 2) return; index = (index + 1) % frames.Count; Show(); timer.Interval = frames[index].Delay; };
        countdown.Tick += (_, _) => { countdown.Interval = Math.Max(50, 1000 - DateTime.Now.Millisecond); Show(); };
        Reload();
    }
    /// Session lock: stop the 1 Hz redraw, nobody can see the tray.
    public bool Paused { get => paused; set { paused = value; ResetCountdown(); } }
    public void Reload()
    {
        timer.Stop(); countdown.Stop(); SetIcon(null);
        foreach (var frame in frames) frame.Image.Dispose(); frames.Clear(); index = 0;
        int inset = settings.Countdown ? RingInset : 0;
        try
        {
            if (settings.Icon is "flutter" or "doze")
            {
                var sequence = settings.Icon == "flutter"
                    ? new[] { ("SparrowIdle", 3000), ("SparrowFlap", 140), ("SparrowIdle", 140), ("SparrowFlap", 140), ("SparrowIdle", 140), ("SparrowFlap", 140) }
                    : new[] { ("SparrowIdle", 3000), ("SparrowClosed", 500), ("SparrowNod", 1000), ("SparrowClosed", 400), ("SparrowNod", 800), ("SparrowClosed", 300) };
                foreach (var (name, delay) in sequence) { using var image = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", name + ".png")); frames.Add((Render(image, true, settings.IconSize, settings.IconZoom, inset), delay)); }
            }
            else
            {
                if (Path.GetFileName(settings.Icon) != settings.Icon) throw new FormatException("아이콘 경로가 올바르지 않습니다.");
                frames.AddRange(ReadCustom(Path.Combine(Folder, settings.Icon), settings.IconSize, settings.IconZoom, inset));
            }
            Show();
            if (settings.Animation && frames.Count > 1) { timer.Interval = frames[0].Delay; timer.Start(); }
            ResetCountdown();
        }
        catch
        {
            foreach (var frame in frames) frame.Image.Dispose(); frames.Clear();
            if (settings.Icon != "flutter") { settings.Icon = "flutter"; Reload(); }
        }
    }
    private void ResetCountdown()
    {
        countdown.Stop();
        if (frames.Count == 0) return;
        Show();
        if (!settings.Countdown || paused) return;
        countdown.Interval = Math.Max(50, 1000 - DateTime.Now.Millisecond); countdown.Start();
    }
    /// Briefly swaps the ring for a checkmark so a click-to-copy has visible feedback.
    public void FlashCopied(int milliseconds = 1200)
    {
        copiedUntil = DateTime.UtcNow.AddMilliseconds(milliseconds); Show();
        var once = new System.Windows.Forms.Timer { Interval = milliseconds + 50 };
        once.Tick += (_, _) => { once.Dispose(); Show(); }; once.Start();
    }
    /// Seconds left in the current OTP window (TOTP windows are aligned to Unix time).
    public static double Remaining(DateTimeOffset now, int period = CountdownPeriod) => period - (now.ToUnixTimeMilliseconds() / 1000.0 % period);
    private void Show()
    {
        if (frames.Count == 0) return;
        SetIcon(Compose(frames[index].Image, DateTimeOffset.UtcNow));
    }
    private void SetIcon(Icon? icon) { var old = shown; shown = icon; tray.Icon = icon ?? SystemIcons.Shield; old?.Dispose(); }
    internal Icon Compose(Bitmap frame, DateTimeOffset now)
    {
        using var bitmap = new Bitmap(Canvas, Canvas); using var g = Graphics.FromImage(bitmap);
        g.DrawImageUnscaled(frame, 0, 0);
        if (settings.Countdown)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(1.5f, 1.5f, Canvas - 3, Canvas - 3);
            bool dark = TaskbarIsDark();
            Color ink = dark ? Color.White : Color.FromArgb(32, 32, 32);
            if (DateTime.UtcNow < copiedUntil)
            {
                using var check = new Pen(Color.FromArgb(16, 185, 80), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawEllipse(check, rect);
            }
            else
            {
                double remaining = Remaining(now);
                using var track = new Pen(Color.FromArgb(70, ink), 3);
                g.DrawEllipse(track, rect);
                using var arc = new Pen(Math.Ceiling(remaining) <= 5 ? Color.FromArgb(232, 64, 64) : ink, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                float sweep = (float)(360 * Math.Clamp(remaining / CountdownPeriod, 0, 1));
                if (sweep > 0.5f) g.DrawArc(arc, rect, -90, sweep);
            }
        }
        IntPtr handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); } finally { DestroyIcon(handle); }
    }
    private static bool TaskbarIsDark()
    {
        try { return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is int light ? light == 0 : true; }
        catch { return true; }
    }
    internal static List<(Bitmap Image, int Delay)> ReadCustom(string file, int size = 100, int zoom = 100, int inset = 0)
    {
        if (!new[] { ".png", ".gif" }.Contains(Path.GetExtension(file).ToLowerInvariant()) || new FileInfo(file).Length > 10 * 1024 * 1024) throw new FormatException("10MB 이하의 PNG 또는 GIF를 선택하세요.");
        using var image = Image.FromFile(file);
        int count = image.FrameDimensionsList.Contains(FrameDimension.Time.Guid) ? image.GetFrameCount(FrameDimension.Time) : 1;
        if (image.Width > 4096 || image.Height > 4096 || count > 120 || (long)image.Width * image.Height * count > 32_000_000) throw new FormatException("아이콘이 너무 큽니다. 최대 4096px, 120프레임, 합계 3,200만 픽셀입니다.");
        byte[]? delays = image.PropertyIdList.Contains(0x5100) ? image.GetPropertyItem(0x5100)?.Value : null;
        var result = new List<(Bitmap, int)>();
        try
        {
            for (int i = 0; i < count; i++)
            {
                if (count > 1) image.SelectActiveFrame(FrameDimension.Time, i);
                int delay = delays != null && delays.Length >= (i + 1) * 4 ? (int)Math.Clamp((long)BitConverter.ToInt32(delays, i * 4) * 10, 60, 10000) : 100;
                result.Add((Render(image, false, size, zoom, inset), delay));
            }
            return result;
        }
        catch { foreach (var (bitmap, _) in result) bitmap.Dispose(); throw; }
    }
    /// Rasterizes one frame onto the 32px tray canvas; with the countdown ring on, the art fits inside the ring (inset).
    private static Bitmap Render(Image image, bool builtin, int size, int zoom, int inset = 0)
    {
        var bitmap = new Bitmap(Canvas, Canvas); using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor; graphics.PixelOffsetMode = PixelOffsetMode.Half;
        float box = Canvas - inset * 2;
        var source = builtin ? new RectangleF(image.Width * .13f, image.Height * .13f, image.Width * .75f, image.Height * .75f) : new RectangleF(0, 0, image.Width, image.Height);
        float scale = Math.Min(box / source.Width, box / source.Height) * Math.Clamp(size, 60, 100) / 100f * Math.Clamp(zoom, 100, 200) / 100f;
        // Clip to the inner box so zoomed art never paints over the ring.
        if (inset > 0) graphics.SetClip(new RectangleF(inset, inset, box, box));
        graphics.DrawImage(image, new RectangleF((Canvas - source.Width * scale) / 2, (Canvas - source.Height * scale) / 2, source.Width * scale, source.Height * scale), source, GraphicsUnit.Pixel);
        return bitmap;
    }
    public void Dispose() { timer.Dispose(); countdown.Dispose(); tray.Icon = null; shown?.Dispose(); foreach (var frame in frames) frame.Image.Dispose(); }
}
