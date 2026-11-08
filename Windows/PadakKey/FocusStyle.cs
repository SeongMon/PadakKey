using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.ComponentModel;

namespace PadakKey;

internal sealed class FocusTheme(bool dark)
{
    private static readonly Dictionary<(float, FontStyle), Font> fonts = [];
    public bool Dark { get; } = dark;
    public Color Surface => Dark ? Color.FromArgb(37, 38, 40) : Color.White;
    public Color Text => Dark ? Color.FromArgb(243, 243, 245) : Color.FromArgb(36, 37, 41);
    public Color Muted => Dark ? Color.FromArgb(166, 169, 177) : Color.FromArgb(112, 117, 125);
    public Color Soft => Dark ? Color.FromArgb(49, 50, 54) : Color.FromArgb(245, 246, 247);
    public Color Line => Dark ? Color.FromArgb(62, 64, 69) : Color.FromArgb(234, 236, 239);
    public Color Accent => Dark ? Color.FromArgb(111, 190, 162) : Color.FromArgb(37, 130, 103);
    public Color Warning => Dark ? Color.FromArgb(235, 175, 105) : Color.FromArgb(166, 102, 28);
    public Font Font(float size = 9, FontStyle style = FontStyle.Regular)
    { lock (fonts) { if (!fonts.TryGetValue((size, style), out var font)) fonts[(size, style)] = font = new Font("Segoe UI", size, style); return font; } }
    public static FocusTheme Current
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return new FocusTheme(key?.GetValue("AppsUseLightTheme") is int light && light == 0);
        }
    }
}

internal static class FocusDraw
{
    public const string Gear = "\uE713", Plus = "\uE710", Copy = "\uE8C8", Back = "\uE72B", Check = "\uE73E", More = "\uE712", Folder = "\uE8B7", Image = "\uE91B", Lock = "\uE72E";
    public static GraphicsPath Round(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (d <= 0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
    public static void Fill(Graphics g, Rectangle bounds, int radius, Color color)
    {
        using var path = Round(bounds, radius); using var brush = new SolidBrush(color); g.FillPath(brush, path);
    }
    public static Bitmap? Sparrow(string selection, int pixels, bool sleepingPreview = false)
    {
        try
        {
            bool builtin = selection is "flutter" or "doze";
            string file = builtin ? Path.Combine(AppContext.BaseDirectory, "Assets", sleepingPreview && selection == "doze" ? "SparrowNod.png" : "SparrowIdle.png")
                : Path.Combine(Mascot.Folder, Path.GetFileName(selection));
            using var image = System.Drawing.Image.FromFile(file);
            var result = new Bitmap(pixels, pixels);
            using var graphics = Graphics.FromImage(result);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor; graphics.PixelOffsetMode = PixelOffsetMode.Half;
            var source = builtin ? new RectangleF(image.Width * .13f, image.Height * .13f, image.Width * .75f, image.Height * .75f) : new RectangleF(0, 0, image.Width, image.Height);
            float scale = Math.Min(pixels / source.Width, pixels / source.Height);
            graphics.DrawImage(image, new RectangleF((pixels - source.Width * scale) / 2, (pixels - source.Height * scale) / 2, source.Width * scale, source.Height * scale), source, GraphicsUnit.Pixel);
            return result;
        }
        catch { return null; }
    }
}

internal class FocusForm : Form
{
    protected readonly FocusTheme Theme;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal bool PreviewMode { get; set; }
    protected override bool ShowWithoutActivation => PreviewMode;
    protected override CreateParams CreateParams
    { get { var parameters = base.CreateParams; parameters.ClassStyle |= 0x20000; if (PreviewMode) parameters.ExStyle |= 0x08000000; return parameters; } }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int length);
    protected int Px(int value) => (int)Math.Round(value * DeviceDpi / 96f);
    public FocusForm(FocusTheme? theme = null)
    {
        Theme = theme ?? FocusTheme.Current;
        AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; BackColor = Theme.Surface; ForeColor = Theme.Text; Font = Theme.Font();
        DoubleBuffered = true; Padding = new Padding(1); KeyPreview = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            int rounded = 2; DwmSetWindowAttribute(Handle, 33, ref rounded, sizeof(int));
            int dark = Theme.Dark ? 1 : 0; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        }
        UpdateCorners();
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateCorners(); }
    private void UpdateCorners()
    {
        if (Width < 1 || Height < 1) return;
        using var path = FocusDraw.Round(new RectangleF(0, 0, Width, Height), Px(22));
        var previous = Region; Region = new Region(path); previous?.Dispose();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = FocusDraw.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), Px(22));
        using var pen = new Pen(Theme.Line); e.Graphics.DrawPath(pen, path);
    }
    protected Label Label(string text, float size = 9, bool bold = false) => new() { Text = text, AutoSize = false, ForeColor = Theme.Text, BackColor = Theme.Surface, Font = Theme.Font(size, bold ? FontStyle.Bold : FontStyle.Regular), AutoEllipsis = true };
    protected FocusButton Button(string text, string glyph = "", bool solid = false) => new(Theme) { Text = text, Glyph = glyph, Solid = solid, Font = Theme.Font() };
}

internal sealed class FocusButton : Button
{
    private readonly FocusTheme theme;
    private bool hover;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal string Glyph { get; set; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal bool Solid { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal bool Quiet { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal bool Selected { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal Image? Preview { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal bool Copied { get; set; }
    public FocusButton(FocusTheme theme)
    {
        this.theme = theme; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        BackColor = theme.Surface; ForeColor = theme.Text; UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        int S(int n) => (int)Math.Round(n * DeviceDpi / 96f);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(theme.Surface);
        Color background = Solid && !Copied ? theme.Text : (Quiet && !hover && !Selected ? theme.Surface : theme.Soft);
        if (!Enabled) background = theme.Soft;
        FocusDraw.Fill(g, new Rectangle(0, 0, Width, Height), S(9), background);
        Color foreground = !Enabled ? theme.Muted : Copied ? theme.Accent : Solid ? theme.Surface : Quiet ? theme.Muted : theme.Text;
        if (Selected) { using var path = FocusDraw.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), S(10)); using var pen = new Pen(theme.Text); g.DrawPath(pen, path); }
        if (Preview != null)
        {
            int size = S(48); g.DrawImage(Preview, new Rectangle((Width - size) / 2, S(12), size, size));
            TextRenderer.DrawText(g, Text, Font, new Rectangle(S(4), Height - S(30), Width - S(8), S(23)), foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Selected) { using var check = new Font("Segoe MDL2 Assets", 8); TextRenderer.DrawText(g, FocusDraw.Check, check, new Rectangle(Width - S(22), S(8), S(15), S(15)), foreground, TextFormatFlags.HorizontalCenter); }
        }
        else
        {
            using var iconFont = new Font("Segoe MDL2 Assets", 10);
            int iconWidth = Glyph.Length > 0 ? S(16) : 0, gap = Glyph.Length > 0 && Text.Length > 0 ? S(6) : 0;
            int textWidth = Text.Length > 0 ? TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width : 0;
            int left = (Width - iconWidth - gap - textWidth) / 2;
            if (Glyph.Length > 0) TextRenderer.DrawText(g, Copied ? FocusDraw.Check : Glyph, iconFont, new Rectangle(left, 0, iconWidth, Height), foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Text.Length > 0) TextRenderer.DrawText(g, Text, Font, new Rectangle(left + iconWidth + gap, 0, Math.Max(textWidth, 1), Height), foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        if (Focused && ShowFocusCues) { using var pen = new Pen(theme.Accent) { DashStyle = DashStyle.Dot }; g.DrawRectangle(pen, S(3), S(3), Width - S(7), Height - S(7)); }
    }
}

internal sealed class FocusToggle : CheckBox
{
    private readonly FocusTheme theme;
    public FocusToggle(FocusTheme theme) { this.theme = theme; AutoSize = false; Size = new(44, 32); Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        float scale = DeviceDpi / 96f; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(theme.Surface);
        int w = (int)(35 * scale), h = (int)(21 * scale), x = (Width - w) / 2, y = (Height - h) / 2;
        FocusDraw.Fill(g, new Rectangle(x, y, w, h), h / 2, Checked ? theme.Accent : theme.Line);
        using var brush = new SolidBrush(Color.White); float d = 15 * scale;
        g.FillEllipse(brush, x + (Checked ? 17 : 3) * scale, y + 3 * scale, d, d);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, ClientRectangle, theme.Accent, theme.Surface);
    }
}

internal sealed class Countdown(FocusTheme theme) : Control
{
    private int seconds = 30, period = 30;
    public void UpdateTime(int remaining, int duration) { if (seconds == remaining && period == duration) return; seconds = remaining; period = duration; AccessibleName = $"{remaining}초 후 코드 갱신"; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(theme.Surface);
        float s = DeviceDpi / 96f; var rect = new RectangleF(3 * s, 3 * s, Width - 6 * s - 1, Height - 6 * s - 1);
        using var track = new Pen(theme.Line, 1.5f * s); using var arc = new Pen(seconds <= 5 ? theme.Warning : theme.Accent, 1.5f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawEllipse(track, rect); g.DrawArc(arc, rect, -90, (float)(360.0 * seconds / period));
        using var font = new Font("Segoe UI", seconds > 99 ? 6.5f : 7.5f);
        TextRenderer.DrawText(g, seconds.ToString(), font, ClientRectangle, theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

internal sealed class FocusSlider : Control
{
    private readonly FocusTheme theme;
    private int value = 100;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value { get => value; set { int next = Math.Clamp(value, 100, 200); if (this.value == next) return; this.value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler? ValueChanged;
    public FocusSlider(FocusTheme theme) { this.theme = theme; TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.Slider; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true); }
    private void SetFromMouse(int x) { float inset = 8f * DeviceDpi / 96; Value = 100 + (int)Math.Round(Math.Clamp((x - inset) / Math.Max(1, Width - 2 * inset), 0, 1) * 100 / 25) * 25; }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Focus(); Capture = true; SetFromMouse(e.X); } base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (Capture) SetFromMouse(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { if (Capture) { SetFromMouse(e.X); Capture = false; } base.OnMouseUp(e); }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Left or Keys.Down) Value -= 25;
        else if (e.KeyCode is Keys.Right or Keys.Up) Value += 25;
        else if (e.KeyCode == Keys.Home) Value = 100;
        else if (e.KeyCode == Keys.End) Value = 200;
        base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(theme.Surface);
        float s = DeviceDpi / 96f, left = 8 * s, right = Width - 8 * s, middle = Height / 2f, position = left + (right - left) * (Value - 100) / 100f;
        using var track = new Pen(theme.Line, 3 * s) { StartCap = LineCap.Round, EndCap = LineCap.Round }; using var active = new Pen(theme.Text, 3 * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(track, left, middle, right, middle); if (position > left) g.DrawLine(active, left, middle, position, middle);
        using var thumb = new SolidBrush(theme.Text); g.FillEllipse(thumb, position - 7 * s, middle - 7 * s, 14 * s, 14 * s);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, ClientRectangle, theme.Accent, theme.Surface);
    }
}

internal static class StartupRegistration
{
    private const string PathName = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(PathName); return key?.GetValue("PadakKey") is string; } }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(PathName);
        if (enabled) key.SetValue("PadakKey", $"\"{Application.ExecutablePath}\""); else key.DeleteValue("PadakKey", false);
    }
}
