using System.Runtime.InteropServices;
using PadakKey.Core;
namespace PadakKey;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--self-test")) { WindowsChecks.Run(); return; }
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
        mascot = new(tray, settings); popup = new(storage, Copy, Add, ShowSettings);
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) { if (popup.Visible) popup.Hide(); else popup.Open(); } };
        clearTimer.Tick += (_, _) => ClearClipboard();
        RebuildMenu();
        Microsoft.Win32.SystemEvents.SessionSwitch += SessionChanged;
    }
    private void SessionChanged(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock && popup.IsHandleCreated) popup.BeginInvoke(() => { popup.Hide(); ClearClipboard(); });
    }
    private void Copy(string code)
    {
        try { Clipboard.SetText(code); clipboardSequence = GetClipboardSequenceNumber(); clearTimer.Stop(); clearTimer.Start(); popup.Hide(); }
        catch (ExternalException) { MessageBox.Show(popup, "다른 앱이 클립보드를 사용 중입니다. 다시 시도하세요."); }
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
            try { var list = storage.Load(); if (list.Any(a => a.Issuer == account.Issuer && a.Name == account.Name && a.Secret == account.Secret)) throw new FormatException("이미 등록한 계정입니다."); list.Add(account); storage.Save(list); }
            catch { MessageBox.Show("계정을 저장할 수 없습니다. 기존 파일은 덮어쓰지 않았습니다."); }
        }
        popup.Open();
    }
    private void ShowSettings() { RebuildMenu(); tray.ContextMenuStrip!.Show(Cursor.Position); }
    private void RebuildMenu()
    {
        var old = tray.ContextMenuStrip; var menu = new ContextMenuStrip();
        menu.Items.Add("인증 코드 열기", null, (_, _) => popup.Open());
        var animation = new ToolStripMenuItem("애니메이션") { Checked = settings.Animation, CheckOnClick = true };
        animation.CheckedChanged += (_, _) => Change(() => settings.Animation = animation.Checked); menu.Items.Add(animation);
        var icons = new ToolStripMenuItem("아이콘 선택");
        void Choice(string title, string id) { var item = new ToolStripMenuItem(title) { Checked = settings.Icon == id }; item.Click += (_, _) => Change(() => settings.Icon = id); icons.DropDownItems.Add(item); }
        Choice("파닥이는 참새", "flutter"); Choice("조는 참새", "doze"); Directory.CreateDirectory(Mascot.Folder);
        foreach (var file in Directory.GetFiles(Mascot.Folder).Where(p => new[] { ".png", ".gif" }.Contains(Path.GetExtension(p).ToLowerInvariant()))) Choice(Path.GetFileNameWithoutExtension(file), Path.GetFileName(file));
        menu.Items.Add(icons);
        var sizes = new ToolStripMenuItem("아이콘 크기");
        foreach (int size in new[] { 60, 75, 90, 100 }) { var item = new ToolStripMenuItem($"{size}%" + (size == 100 ? " (최대)" : "")) { Checked = settings.IconSize == size }; item.Click += (_, _) => Change(() => settings.IconSize = size); sizes.DropDownItems.Add(item); }
        menu.Items.Add(sizes);
        menu.Items.Add("아이콘 추가…", null, (_, _) =>
        {
            popup.Hide(); using var dialog = new OpenFileDialog { Filter = "아이콘|*.png;*.gif" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            try
            {
                var test = Mascot.ReadCustom(dialog.FileName); foreach (var frame in test) frame.Icon.Dispose();
                string name = Path.GetFileNameWithoutExtension(dialog.FileName), ext = Path.GetExtension(dialog.FileName); string dest = Path.Combine(Mascot.Folder, name + ext); int n = 2;
                while (File.Exists(dest)) dest = Path.Combine(Mascot.Folder, name + "-" + n++ + ext);
                File.Copy(dialog.FileName, dest); Change(() => settings.Icon = Path.GetFileName(dest));
            }
            catch { MessageBox.Show("아이콘을 추가할 수 없습니다. PNG/GIF 형식과 크기를 확인하세요."); }
        });
        menu.Items.Add("아이콘 폴더 열기", null, (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Mascot.Folder) { UseShellExecute = true }));
        menu.Items.Add("종료", null, (_, _) => ExitThread());
        tray.ContextMenuStrip = menu; old?.Dispose();
    }
    private void Change(Action change) { change(); mascot.Reload(); try { settings.Save(); } catch { MessageBox.Show("설정을 저장할 수 없습니다."); } }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Microsoft.Win32.SystemEvents.SessionSwitch -= SessionChanged; ClearClipboard(); clearTimer.Dispose(); mascot.Dispose(); tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); popup.Dispose(); }
        base.Dispose(disposing);
    }
}
internal sealed class Popup : Form
{
    private readonly Storage storage;
    private readonly Action<string> copy;
    private readonly TextBox search = new() { Dock = DockStyle.Top, PlaceholderText = "서비스 또는 계정 검색" };
    private readonly FlowLayoutPanel rows = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly System.Windows.Forms.Timer tick = new() { Interval = 250 };
    private List<Account> accounts = [];
    private readonly List<(Account Account, Button Code)> codes = [];
    public Popup(Storage storage, Action<string> copy, Action add, Action settings)
    {
        this.storage = storage; this.copy = copy;
        Text = "PadakKey"; ShowInTaskbar = false; TopMost = true; FormBorderStyle = FormBorderStyle.FixedToolWindow; StartPosition = FormStartPosition.Manual; ClientSize = new(370, 380); BackColor = Color.FromArgb(238, 241, 244); Font = new Font("Segoe UI", 10);
        var top = new Panel { Dock = DockStyle.Top, Height = 52 }; top.Controls.Add(new Label { Text = "PadakKey", Left = 14, Top = 14, AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        var plus = new Button { Text = "+", Dock = DockStyle.Right, Width = 44 }; plus.Click += (_, _) => add(); top.Controls.Add(plus);
        var bottom = new Button { Text = "⚙ 설정 · 복사 후 30초 뒤 자동 지움", Dock = DockStyle.Bottom, Height = 36 }; bottom.Click += (_, _) => settings();
        Controls.Add(rows); Controls.Add(search); Controls.Add(top); Controls.Add(bottom);
        search.TextChanged += (_, _) => Render(); tick.Tick += (_, _) => UpdateCodes();
        Deactivate += (_, _) => Hide();
        VisibleChanged += (_, _) => { if (!Visible) { tick.Stop(); accounts.Clear(); codes.Clear(); ClearRows(); } };
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
    }
    public void Open()
    {
        try { accounts = storage.Load(); search.Text = ""; Render(); }
        catch { accounts.Clear(); ClearRows(); rows.Controls.Add(new Label { Text = "계정을 열 수 없습니다. Windows 사용자 또는 계정 파일을 확인하세요. 파일은 변경하지 않았습니다.", Width = 320, Height = 100 }); }
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(Math.Clamp(Cursor.Position.X - Width, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(Cursor.Position.Y - Height, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        Show(); Activate(); tick.Start();
    }
    private void ClearRows() { foreach (Control row in rows.Controls.Cast<Control>().ToArray()) { rows.Controls.Remove(row); row.Dispose(); } }
    private void Render()
    {
        ClearRows(); codes.Clear();
        foreach (var account in accounts.Where(a => (a.Issuer + " " + a.Name).Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
        {
            var row = new Panel { Width = 335, Height = 110, Margin = new Padding(8), BackColor = Color.FromArgb(220, 225, 230) };
            row.Controls.Add(new Label { Text = (account.Issuer.Length == 0 ? "인증 계정" : account.Issuer) + "\n" + account.Name, Left = 10, Top = 8, Width = 270, Height = 42, AutoEllipsis = true });
            var button = new Button { Left = 10, Top = 52, Width = 310, Height = 46, FlatStyle = FlatStyle.Flat, Font = new Font("Consolas", 17) };
            button.Click += (_, _) => { try { copy(account.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds())); } catch { MessageBox.Show("코드를 생성할 수 없습니다."); } }; row.Controls.Add(button); codes.Add((account, button));
            var delete = new Button { Text = "×", Left = 292, Top = 6, Width = 32 };
            delete.Click += (_, _) =>
            {
                Hide();
                if (MessageBox.Show("이 PC에서 계정을 삭제할까요? 다른 인증 수단이 있는지 확인하세요.", "계정 삭제", MessageBoxButtons.YesNo) == DialogResult.Yes)
                { try { var next = storage.Load(); next.RemoveAll(a => a.Id == account.Id); storage.Save(next); } catch { MessageBox.Show("삭제할 수 없습니다."); } }
                Open();
            };
            row.Controls.Add(delete); rows.Controls.Add(row);
        }
        if (codes.Count == 0) rows.Controls.Add(new Label { Text = accounts.Count == 0 ? "+ 버튼으로 첫 계정을 등록하세요." : "검색 결과가 없습니다.", Width = 320, Height = 60, Padding = new Padding(12) });
        UpdateCodes();
    }
    private void UpdateCodes() { long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); foreach (var (a, button) in codes) { string code = a.Code(now); button.Text = code.Insert(code.Length / 2, " ") + $"   {a.Period - now % a.Period}s"; } }
    protected override void Dispose(bool disposing) { if (disposing) tick.Dispose(); base.Dispose(disposing); }
}
