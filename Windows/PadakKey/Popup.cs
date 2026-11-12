using PadakKey.Core;

namespace PadakKey;

internal sealed class Popup : FocusForm
{
    private readonly Storage storage;
    private readonly Action<string> copy;
    private readonly Func<string> selectedIcon;
    private readonly PictureBox bird;
    private readonly Label brand, footerNote, empty, startupLabel;
    private readonly FocusButton settingsButton, addButton;
    private readonly FocusToggle startupToggle;
    private readonly Func<bool> startupEnabled;
    private readonly Action<bool> setStartup;
    private bool startupPreview;
    private readonly TextBox search;
    private readonly Panel searchFrame;
    private readonly FlowLayoutPanel rows;
    private readonly System.Windows.Forms.Timer tick = new() { Interval = 250 };
    private readonly List<OtpRow> codeRows = [];
    private List<Account> accounts = [];
    private bool preparing, loadFailed, searchShown;
    private Point anchor;
    internal int VisibleAccountCount => codeRows.Count;

    // Startup accessors default to an in-memory value so previews and checks never touch the real Run key.
    public Popup(Storage storage, Action<string> copy, Action add, Action settings, Func<string>? selectedIcon = null, FocusTheme? theme = null, Func<bool>? startupEnabled = null, Action<bool>? setStartup = null) : base(theme)
    {
        this.storage = storage; this.copy = copy; this.selectedIcon = selectedIcon ?? (() => "flutter");
        this.startupEnabled = startupEnabled ?? (() => startupPreview); this.setStartup = setStartup ?? (value => startupPreview = value);
        Text = "PadakKey"; AccessibleName = "PadakKey 인증 코드";
        bird = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, TabStop = false };
        brand = Label("padakkey", 10.5f, true);
        settingsButton = Button("", FocusDraw.Gear); settingsButton.Quiet = true; settingsButton.AccessibleName = "설정 및 아이콘 변경";
        settingsButton.Click += (_, _) => settings();
        addButton = Button("OTP 추가", FocusDraw.Plus); addButton.Quiet = true; addButton.AccessibleName = "OTP 추가"; addButton.Click += (_, _) => add();
        footerNote = Label("코드를 클릭하면 복사됩니다", 7.5f); footerNote.ForeColor = Theme.Muted; footerNote.TextAlign = ContentAlignment.MiddleRight;
        searchFrame = new Panel { BackColor = Theme.Soft };
        search = new TextBox { BorderStyle = BorderStyle.None, PlaceholderText = "서비스 또는 계정 검색", BackColor = Theme.Soft, ForeColor = Theme.Text, Font = Theme.Font(9), AccessibleName = "서비스 또는 계정 검색" };
        searchFrame.Controls.Add(search);
        rows = new FlowLayoutPanel { AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Surface, Margin = Padding.Empty, Padding = Padding.Empty };
        empty = Label("", 9); empty.TextAlign = ContentAlignment.MiddleCenter; empty.Margin = Padding.Empty;
        startupLabel = Label("시작 시 실행", 8); startupLabel.ForeColor = Theme.Muted; startupLabel.TextAlign = ContentAlignment.MiddleRight;
        startupToggle = new FocusToggle(Theme) { AccessibleName = "Windows 시작 시 실행", TabStop = true };
        startupToggle.CheckedChanged += (_, _) =>
        {
            if (preparing || startupToggle.Checked == this.startupEnabled()) return;
            try { this.setStartup(startupToggle.Checked); }
            catch { preparing = true; startupToggle.Checked = this.startupEnabled(); preparing = false; footerNote.Text = "자동 실행 설정을 저장할 수 없습니다"; footerNote.Visible = true; }
        };
        startupLabel.Click += (_, _) => startupToggle.Checked = !startupToggle.Checked;
        Controls.AddRange([bird, brand, startupLabel, startupToggle, settingsButton, searchFrame, rows, addButton, footerNote]);
        search.TextChanged += (_, _) => { if (!preparing) Render(); };
        tick.Tick += (_, _) => UpdateCodes();
        Deactivate += (_, _) => { if (!preparing) Hide(); };
        VisibleChanged += (_, _) => { if (!Visible) { tick.Stop(); accounts.Clear(); ClearRows(); } };
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = true; } if (e.Control && e.KeyCode == Keys.F) { searchShown = true; searchFrame.Visible = true; LayoutContent(); PositionAtAnchor(); search.Focus(); e.Handled = true; } };
        ClientSize = new(370, 370);
    }

    public void Open() => OpenAt(Cursor.Position);
    public void Reopen() { if (anchor == Point.Empty) Open(); else OpenAt(anchor); }
    private void OpenAt(Point point)
    {
        anchor = point; LoadAccounts();
        PositionAtAnchor();
        Show(); Activate(); settingsButton.Focus(); tick.Start();
    }
    internal void PreparePreview() { anchor = new Point(1000, 1000); LoadAccounts(); }
    private void PositionAtAnchor()
    {
        if (PreviewMode) return;
        var area = Screen.FromPoint(anchor).WorkingArea;
        Location = new Point(Math.Clamp(anchor.X - Width, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(anchor.Y - Height, area.Top, Math.Max(area.Top, area.Bottom - Height)));
    }
    private void LoadAccounts()
    {
        preparing = true; loadFailed = false;
        try { accounts = storage.Load(); }
        catch { accounts = []; loadFailed = true; }
        search.Text = ""; footerNote.Text = "코드를 클릭하면 복사됩니다";
        try { startupToggle.Checked = startupEnabled(); } catch { startupToggle.Checked = false; }
        bird.Image?.Dispose(); bird.Image = FocusDraw.Sparrow(selectedIcon(), Px(28));
        preparing = false; Render();
    }
    private void ClearRows()
    {
        foreach (Control row in rows.Controls.Cast<Control>().ToArray()) { rows.Controls.Remove(row); if (row != empty) row.Dispose(); }
        codeRows.Clear(); rows.AutoScrollPosition = Point.Empty;
    }
    private void Render()
    {
        rows.SuspendLayout(); ClearRows();
        var visible = accounts.Where(a => (a.Issuer + " " + a.Name).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var account in visible)
        {
            var row = new OtpRow(Theme, account, codeRows.Count == 0, code => copy(code), () => DeleteAccount(account));
            codeRows.Add(row); rows.Controls.Add(row);
        }
        searchShown = accounts.Count > 3 || search.Text.Length > 0; searchFrame.Visible = searchShown;
        if (codeRows.Count == 0)
        {
            empty.Text = loadFailed ? "계정을 열 수 없습니다.\nWindows 사용자와 계정 파일을 확인하세요.\n기존 파일은 변경하지 않았습니다."
                : accounts.Count == 0 ? "첫 번째 OTP를 추가하세요.\nQR 이미지나 인증 키로 시작할 수 있어요." : "검색 결과가 없습니다.";
            rows.Controls.Add(empty);
        }
        footerNote.Visible = codeRows.Count > 0;
        LayoutContent(); rows.ResumeLayout(true); UpdateCodes(); if (Visible) PositionAtAnchor(); Invalidate();
    }
    private void DeleteAccount(Account account)
    {
        Hide();
        if (MessageBox.Show("이 PC에서 계정을 삭제할까요? 다른 인증 수단이나 복구 코드가 있는지 확인하세요.", "계정 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try { var next = storage.Load(); next.RemoveAll(a => a.Id == account.Id); storage.Save(next); }
            catch { MessageBox.Show("계정을 삭제할 수 없습니다."); }
        }
        Reopen();
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); if (rows != null && !preparing) LayoutContent(false); }
    private void LayoutContent(bool resize = true)
    {
        int inset = Px(24), contentWidth = ClientSize.Width - 2 * inset;
        int top = Px(64) + (searchShown ? Px(46) : 0);
        int requested = codeRows.Count == 0 ? Px(214) : Px(228) + Math.Min(codeRows.Count - 1, 2) * Px(148);
        int maxHeight = Screen.FromPoint(anchor).WorkingArea.Height - Px(32);
        int body = Math.Max(Px(80), Math.Min(requested, maxHeight - top - Px(64)));
        if (resize) { preparing = true; ClientSize = new Size(Px(370), top + body + Px(64)); preparing = false; contentWidth = ClientSize.Width - 2 * inset; }
        bird.SetBounds(inset, Px(20), Px(27), Px(27));
        brand.SetBounds(inset + Px(36), Px(21), contentWidth - Px(196), Px(25));
        settingsButton.SetBounds(ClientSize.Width - inset - Px(32), Px(18), Px(32), Px(32));
        startupToggle.SetBounds(settingsButton.Left - Px(48), Px(18), Px(44), Px(32));
        startupLabel.SetBounds(startupToggle.Left - Px(80), Px(18), Px(80), Px(32));
        searchFrame.SetBounds(inset, Px(61), contentWidth, Px(33));
        search.SetBounds(Px(10), Px(7), contentWidth - Px(20), Px(23));
        rows.SetBounds(inset, top, contentWidth, body);
        int rowWidth = contentWidth - (codeRows.Count > 3 ? SystemInformation.VerticalScrollBarWidth : 0);
        foreach (var row in codeRows) row.Size = new Size(rowWidth, Px(row.Featured ? 228 : 148));
        empty.Size = new Size(contentWidth, body);
        addButton.SetBounds(inset - Px(4), ClientSize.Height - Px(48), Px(106), Px(32));
        footerNote.SetBounds(ClientSize.Width - inset - Px(190), ClientSize.Height - Px(48), Px(190), Px(32));
    }
    private void UpdateCodes() { long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); foreach (var row in codeRows) row.UpdateCode(now); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); if (addButton == null) return;
        using var pen = new Pen(Theme.Line); e.Graphics.DrawLine(pen, Px(24), ClientSize.Height - Px(61), ClientSize.Width - Px(24), ClientSize.Height - Px(61));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { tick.Dispose(); bird.Image?.Dispose(); if (!empty.IsDisposed) empty.Dispose(); }
        base.Dispose(disposing);
    }
}

internal sealed class OtpRow : Panel
{
    private readonly FocusTheme theme;
    private readonly Account account;
    private readonly Label service, name;
    private readonly FocusButton more;
    private readonly OtpCodeButton code, nextCode;
    private readonly Action<string> copy;
    private readonly Func<long> timeSource;
    public bool Featured { get; }
    private int Px(int value) => (int)Math.Round(value * DeviceDpi / 96f);
    public OtpRow(FocusTheme theme, Account account, bool featured, Action<string> copy, Action delete, Func<long>? timeSource = null)
    {
        this.theme = theme; this.account = account; this.copy = copy; Featured = featured;
        this.timeSource = timeSource ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        DoubleBuffered = true; Margin = Padding.Empty; BackColor = theme.Surface;
        service = new Label { Text = account.Issuer.Length > 0 ? account.Issuer : "인증 계정", AutoEllipsis = true, Font = theme.Font(featured ? 10 : 9, FontStyle.Bold), ForeColor = theme.Text };
        name = new Label { Text = account.Name, AutoEllipsis = true, Font = theme.Font(8), ForeColor = theme.Muted };
        code = new OtpCodeButton(theme) { Font = new Font("Consolas", account.Digits == 8 ? 15 : 19), AccessibleName = service.Text + " 현재 OTP 복사" };
        code.Click += (_, _) => CopyCode();
        nextCode = new OtpCodeButton(theme, next: true) { Font = new Font("Consolas", account.Digits == 8 ? 15 : 19), AccessibleName = service.Text + " 다음 OTP 복사", AccessibleDescription = "다음 갱신 주기부터 사용할 수 있는 코드를 복사합니다." };
        nextCode.Click += (_, _) => CopyCode(next: true);
        more = new FocusButton(theme) { Glyph = FocusDraw.More, Quiet = true, AccessibleName = service.Text + " 계정 관리" };
        more.Click += (_, _) => { var menu = new ContextMenuStrip { Font = theme.Font() }; var ui = SynchronizationContext.Current; void Later(Action action) { if (ui != null) ui.Post(_ => action(), null); else action(); } menu.Items.Add("계정 삭제…", null, (_, _) => Later(delete)); menu.Closed += (_, _) => Later(menu.Dispose); menu.Show(more, new Point(0, more.Height)); };
        Controls.AddRange([service, name, code, nextCode, more]);
    }
    private void CopyCode(bool next = false)
    {
        try { long now = timeSource(); copy(next ? account.NextCode(now) : account.Code(now)); }
        catch { MessageBox.Show("코드를 복사할 수 없습니다. 다시 시도하세요."); }
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e); if (service == null) return;
        if (Featured)
        {
            service.TextAlign = name.TextAlign = ContentAlignment.MiddleCenter;
            service.SetBounds(Px(24), Px(66), Width - Px(48), Px(23));
            name.SetBounds(Px(14), Px(90), Width - Px(28), Px(20));
            more.SetBounds(Width - Px(25), Px(8), Px(25), Px(25));
        }
        else
        {
            service.SetBounds(0, Px(14), Width - Px(32), Px(20)); name.SetBounds(0, Px(36), Width - Px(32), Px(18));
            more.SetBounds(Width - Px(25), Px(19), Px(25), Px(25));
        }
        int cardWidth = (Width - Px(10)) / 2, top = Px(Featured ? 116 : 60);
        code.SetBounds(0, top, cardWidth, Px(76));
        nextCode.SetBounds(cardWidth + Px(10), top, cardWidth, Px(76));
    }
    public void UpdateCode(long now)
    {
        try
        {
            string current = account.Code(now); string grouped = current.Insert(current.Length / 2, " ");
            if (code.Text != grouped) code.Text = grouped;
            string following = account.NextCode(now); string groupedNext = following.Insert(following.Length / 2, " ");
            if (nextCode.Text != groupedNext) nextCode.Text = groupedNext;
            code.UpdateTime(account.Period - (int)(now % account.Period), account.Period);
        }
        catch { code.Text = "코드 오류"; code.Enabled = nextCode.Enabled = false; }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Featured)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(theme.Soft); e.Graphics.FillEllipse(fill, (Width - Px(43)) / 2, Px(9), Px(43), Px(43));
            using var font = new Font("Segoe MDL2 Assets", 13); TextRenderer.DrawText(e.Graphics, FocusDraw.Lock, font, new Rectangle((Width - Px(43)) / 2, Px(9), Px(43), Px(43)), theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        else { using var pen = new Pen(theme.Line); e.Graphics.DrawLine(pen, 0, 0, Width, 0); }
    }
    protected override void Dispose(bool disposing) { if (disposing) { code.Font.Dispose(); nextCode.Font.Dispose(); } base.Dispose(disposing); }
}

internal sealed class OtpCodeButton : Button
{
    private readonly FocusTheme theme;
    private bool hover;
    private int seconds = 30, period = 30;
    internal bool IsNext { get; }
    internal Rectangle TimerBounds => IsNext ? Rectangle.Empty : new Rectangle(Width - Px(34), Px(7), Px(26), Px(26));
    private int Px(int n) => (int)Math.Round(n * DeviceDpi / 96f);
    internal void UpdateTime(int remaining, int duration)
    {
        if (seconds == remaining && period == duration) return;
        seconds = remaining; period = duration;
        AccessibleDescription = $"{remaining}초 후 코드 갱신. 박스를 누르면 복사합니다.";
        Invalidate();
    }
    public OtpCodeButton(FocusTheme theme, bool next = false)
    {
        this.theme = theme; IsNext = next; Text = "—"; Cursor = Cursors.Hand; FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0; BackColor = next ? theme.Soft : theme.Surface; ForeColor = next ? theme.Muted : theme.Text; UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.Clear(theme.Surface);
        FocusDraw.Fill(g, ClientRectangle, Px(9), hover ? theme.Line : BackColor);
        using var path = FocusDraw.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), Px(9));
        using var border = new Pen(theme.Line); g.DrawPath(border, path);
        TextRenderer.DrawText(g, IsNext ? "다음" : "현재", theme.Font(8), new Rectangle(Px(10), Px(8), Width - Px(50), Px(24)), theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(Px(10), Px(35), Width - Px(20), Px(33)), Enabled ? ForeColor : theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (!IsNext)
        {
            var bounds = TimerBounds; float scale = DeviceDpi / 96f;
            var ring = new RectangleF(bounds.X + 2 * scale, bounds.Y + 2 * scale, bounds.Width - 4 * scale, bounds.Height - 4 * scale);
            using var track = new Pen(theme.Line, 1.5f * scale);
            using var arc = new Pen(seconds <= 5 ? theme.Warning : theme.Accent, 1.5f * scale);
            g.DrawEllipse(track, ring); g.DrawArc(arc, ring, -90, 360f * seconds / period);
            TextRenderer.DrawText(g, seconds.ToString(), theme.Font(seconds > 99 ? 6.5f : 7.5f), bounds, theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -Px(3), -Px(3)), theme.Accent, BackColor);
    }
}
