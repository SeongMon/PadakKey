using System.Runtime.InteropServices;
namespace PadakKey;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--self-test")) { WindowsChecks.Run(); return; }
        if (args.Length == 2 && args[0] == "--ui-preview") { FocusChecks.Render(args[1]); return; }
        using var mutex = new Mutex(true, @"Local\PadakKey.Tray", out bool first);
        if (!first) { MessageBox.Show("PadakKey가 이미 실행 중입니다. 오른쪽 아래 트레이 아이콘을 확인하세요."); return; }
        using var context = new TrayApp(); Application.Run(context);
    }
}

internal sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon tray = new() { Text = "PadakKey", Visible = true, Icon = SystemIcons.Shield };
    private readonly Settings settings = Settings.Load();
    private readonly Storage storage = new(Storage.Root);
    private readonly Mascot mascot;
    private readonly Popup popup;
    private readonly System.Windows.Forms.Timer clearTimer = new() { Interval = 30000 };
    private uint clipboardSequence;
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    public TrayApp()
    {
        mascot = new(tray, settings); popup = new(storage, Copy, Add, ShowSettings, () => settings.Icon);
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) { if (popup.Visible) popup.Hide(); else popup.Open(); } };
        clearTimer.Tick += (_, _) => ClearClipboard(); RebuildMenu();
        Microsoft.Win32.SystemEvents.SessionSwitch += SessionChanged;
    }
    private void SessionChanged(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock && popup.IsHandleCreated) popup.BeginInvoke(() => { popup.Hide(); ClearClipboard(); });
    }
    private void Copy(string code)
    {
        try { Clipboard.SetText(code); clipboardSequence = GetClipboardSequenceNumber(); clearTimer.Stop(); clearTimer.Start(); }
        catch (ExternalException ex) { throw new InvalidOperationException("다른 앱이 클립보드를 사용 중입니다.", ex); }
    }
    private void ClearClipboard()
    {
        clearTimer.Stop();
        try { if (clipboardSequence != 0 && clipboardSequence == GetClipboardSequenceNumber()) Clipboard.Clear(); } catch (ExternalException) { }
        clipboardSequence = 0;
    }
    private void Add()
    {
        popup.Hide(); using var dialog = new Registration();
        if (dialog.ShowDialog() == DialogResult.OK && dialog.Result is { } account)
        {
            try
            {
                var list = storage.Load();
                if (list.Any(a => a.Issuer == account.Issuer && a.Name == account.Name && a.Secret == account.Secret)) throw new FormatException("이미 등록한 계정입니다.");
                list.Add(account); storage.Save(list);
            }
            catch (FormatException ex) { MessageBox.Show(ex.Message); }
            catch { MessageBox.Show("계정을 저장할 수 없습니다. 기존 파일은 덮어쓰지 않았습니다."); }
        }
        popup.Reopen();
    }
    private void ShowSettings()
    {
        Point location = popup.Location; popup.Hide();
        using var dialog = new FocusSettings(settings, Change, ImportIcon);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        dialog.Location = new Point(Math.Clamp(location.X, area.Left, Math.Max(area.Left, area.Right - dialog.Width)), Math.Clamp(location.Y, area.Top, Math.Max(area.Top, area.Bottom - dialog.Height)));
        dialog.ShowDialog(); RebuildMenu(); popup.Reopen();
    }
    private bool ImportIcon(IWin32Window? owner)
    {
        using var dialog = new OpenFileDialog { Filter = "참새 및 사용자 아이콘|*.png;*.gif", Title = "아이콘 추가" };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
        try
        {
            var test = Mascot.ReadCustom(dialog.FileName); foreach (var frame in test) frame.Icon.Dispose();
            Directory.CreateDirectory(Mascot.Folder);
            string name = Path.GetFileNameWithoutExtension(dialog.FileName), ext = Path.GetExtension(dialog.FileName), dest = Path.Combine(Mascot.Folder, name + ext); int n = 2;
            while (File.Exists(dest)) dest = Path.Combine(Mascot.Folder, name + "-" + n++ + ext);
            File.Copy(dialog.FileName, dest); Change(() => settings.Icon = Path.GetFileName(dest)); return true;
        }
        catch { MessageBox.Show(owner, "아이콘을 추가할 수 없습니다. PNG/GIF 형식과 크기를 확인하세요."); return false; }
    }
    private void RebuildMenu()
    {
        var old = tray.ContextMenuStrip; var menu = new ContextMenuStrip { Font = new Font("Segoe UI", 9) };
        menu.Items.Add("인증 코드 열기", null, (_, _) => popup.Open());
        menu.Items.Add("설정 및 아이콘 변경…", null, (_, _) => ShowSettings()); menu.Items.Add(new ToolStripSeparator());
        var animation = new ToolStripMenuItem("애니메이션") { Checked = settings.Animation, CheckOnClick = true };
        animation.CheckedChanged += (_, _) => { if (settings.Animation != animation.Checked) Change(() => settings.Animation = animation.Checked); }; menu.Items.Add(animation);
        var icons = new ToolStripMenuItem("아이콘 선택");
        void Choice(string title, string id) { var item = new ToolStripMenuItem(title) { Checked = settings.Icon == id, Tag = id }; item.Click += (_, _) => Change(() => settings.Icon = id); icons.DropDownItems.Add(item); }
        Choice("파닥이는 참새", "flutter"); Choice("조는 참새", "doze"); Directory.CreateDirectory(Mascot.Folder);
        foreach (var file in Directory.GetFiles(Mascot.Folder).Where(p => new[] { ".png", ".gif" }.Contains(Path.GetExtension(p).ToLowerInvariant()))) Choice(Path.GetFileNameWithoutExtension(file), Path.GetFileName(file));
        menu.Items.Add(icons);
        var sizes = new ToolStripMenuItem("아이콘 크기");
        foreach (int size in new[] { 60, 75, 90, 100 }) { var item = new ToolStripMenuItem($"{size}%" + (size == 100 ? " (최대)" : "")) { Checked = settings.IconSize == size, Tag = size }; item.Click += (_, _) => Change(() => settings.IconSize = size); sizes.DropDownItems.Add(item); }
        menu.Items.Add(sizes);
        var zooms = new ToolStripMenuItem("아이콘 확대 (여백 줄이기)");
        foreach (int zoom in new[] { 100, 125, 150, 175, 200 }) { var item = new ToolStripMenuItem($"{zoom}%") { Checked = settings.IconZoom == zoom, Tag = zoom }; item.Click += (_, _) => Change(() => settings.IconZoom = zoom); zooms.DropDownItems.Add(item); }
        menu.Items.Add(zooms);
        menu.Items.Add("아이콘 추가…", null, (_, _) => { popup.Hide(); if (ImportIcon(null)) RebuildMenu(); });
        menu.Items.Add("아이콘 폴더 열기", null, (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Mascot.Folder) { UseShellExecute = true }));
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("종료", null, (_, _) => ExitThread());
        menu.Opening += (_, _) =>
        {
            animation.Checked = settings.Animation;
            foreach (ToolStripMenuItem item in zooms.DropDownItems) item.Checked = (int?)item.Tag == settings.IconZoom;
            foreach (ToolStripMenuItem item in icons.DropDownItems) item.Checked = (string?)item.Tag == settings.Icon;
            foreach (ToolStripMenuItem item in sizes.DropDownItems) item.Checked = (int?)item.Tag == settings.IconSize;
        };
        tray.ContextMenuStrip = menu; old?.Dispose();
    }
    private void Change(Action change) { change(); mascot.Reload(); try { settings.Save(); } catch { MessageBox.Show("설정을 저장할 수 없습니다."); } }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Microsoft.Win32.SystemEvents.SessionSwitch -= SessionChanged; ClearClipboard(); clearTimer.Dispose(); mascot.Dispose(); tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); popup.Dispose(); }
        base.Dispose(disposing);
    }
}
