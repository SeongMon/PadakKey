using PadakKey.Core;

namespace PadakKey;

internal sealed class Popup : FocusForm
{
    private readonly Storage storage;
    private readonly Action<string> copy;
    private readonly Func<string> selectedIcon;
    private readonly PictureBox bird;
    private readonly Label brand, footerNote, empty;
    private readonly FocusButton settingsButton, addButton;
    private readonly TextBox search;
    private readonly Panel searchFrame;
    private readonly FlowLayoutPanel rows;
    private readonly System.Windows.Forms.Timer tick = new() { Interval = 250 };
    private readonly List<OtpRow> codeRows = [];
    private List<Account> accounts = [];
    private bool preparing, loadFailed, searchShown;
    private Point anchor;
    internal int VisibleAccountCount => codeRows.Count;

    public Popup(Storage storage, Action<string> copy, Action add, Action settings, Func<string>? selectedIcon = null, FocusTheme? theme = null) : base(theme)
    {
        this.storage = storage; this.copy = copy; this.selectedIcon = selectedIcon ?? (() => "flutter");
        Text = "PadakKey"; AccessibleName = "PadakKey 인증 코드";
        bird = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, TabStop = false };
        brand = Label("padakkey", 10.5f, true);
        settingsButton = Button("", FocusDraw.Gear); settingsButton.Quiet = true; settingsButton.AccessibleName = "설정 및 아이콘 변경";
        settingsButton.Click += (_, _) => settings();
        addButton = Button("OTP 추가", FocusDraw.Plus); addButton.Quiet = true; addButton.AccessibleName = "OTP 추가"; addButton.Click += (_, _) => add();
        footerNote = Label("실시간 자동 갱신", 7.5f); footerNote.ForeColor = Theme.Muted; footerNote.TextAlign = ContentAlignment.MiddleRight;
        searchFrame = new Panel { BackColor = Theme.Soft };
        search = new TextBox { BorderStyle = BorderStyle.None, PlaceholderText = "서비스 또는 계정 검색", BackColor = Theme.Soft, ForeColor = Theme.Text, Font = Theme.Font(9), AccessibleName = "서비스 또는 계정 검색" };
        searchFrame.Controls.Add(search);
        rows = new FlowLayoutPanel { AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Surface, Margin = Padding.Empty, Padding = Padding.Empty };
        empty = Label("", 9); empty.TextAlign = ContentAlignment.MiddleCenter; empty.Margin = Padding.Empty;
        Controls.AddRange([bird, brand, settingsButton, searchFrame, rows, addButton, footerNote]);
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
        search.Text = "";
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
        int requested = codeRows.Count == 0 ? Px(214) : Px(228) + Math.Min(codeRows.Count - 1, 2) * Px(112);
        int maxHeight = Screen.FromPoint(anchor).WorkingArea.Height - Px(32);
        int body = Math.Max(Px(80), Math.Min(requested, maxHeight - top - Px(64)));
        if (resize) { preparing = true; ClientSize = new Size(Px(370), top + body + Px(64)); preparing = false; contentWidth = ClientSize.Width - 2 * inset; }
        bird.SetBounds(inset, Px(20), Px(27), Px(27));
        brand.SetBounds(inset + Px(36), Px(21), contentWidth - Px(80), Px(25));
        settingsButton.SetBounds(ClientSize.Width - inset - Px(32), Px(18), Px(32), Px(32));
        searchFrame.SetBounds(inset, Px(61), contentWidth, Px(33));
        search.SetBounds(Px(10), Px(7), contentWidth - Px(20), Px(23));
        rows.SetBounds(inset, top, contentWidth, body);
        int rowWidth = contentWidth - (codeRows.Count > 3 ? SystemInformation.VerticalScrollBarWidth : 0);
        foreach (var row in codeRows) row.Size = new Size(rowWidth, Px(row.Featured ? 228 : 112));
        empty.Size = new Size(contentWidth, body);
        addButton.SetBounds(inset - Px(4), ClientSize.Height - Px(48), Px(106), Px(32));
        footerNote.SetBounds(ClientSize.Width - inset - Px(150), ClientSize.Height - Px(48), Px(150), Px(32));
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
    private readonly Label service, name, code;
    private readonly FocusButton copyButton, more;
    private readonly Countdown countdown;
    private readonly Action<string> copy;
    private readonly Label? refresh;
    public bool Featured { get; }
    private int Px(int value) => (int)Math.Round(value * DeviceDpi / 96f);
    public OtpRow(FocusTheme theme, Account account, bool featured, Action<string> copy, Action delete)
    {
        this.theme = theme; this.account = account; this.copy = copy; Featured = featured;
        DoubleBuffered = true; Margin = Padding.Empty; BackColor = theme.Surface;
        service = new Label { Text = account.Issuer.Length > 0 ? account.Issuer : "인증 계정", AutoEllipsis = true, Font = theme.Font(featured ? 10 : 9, FontStyle.Bold), ForeColor = theme.Text };
        name = new Label { Text = account.Name, AutoEllipsis = true, Font = theme.Font(8), ForeColor = theme.Muted };
        code = new Label { Font = new Font("Consolas", featured ? (account.Digits == 8 ? 25 : 29) : (account.Digits == 8 ? 15 : 19)), ForeColor = theme.Text, Cursor = Cursors.Hand, AccessibleName = "현재 OTP 코드; 복사 버튼으로 복사" };
        code.Click += (_, _) => CopyCode();
        copyButton = new FocusButton(theme) { Text = "복사", Glyph = FocusDraw.Copy, Solid = featured, Font = theme.Font(featured ? 8.5f : 8), AccessibleName = service.Text + " OTP 복사" };
        copyButton.Click += (_, _) => CopyCode();
        more = new FocusButton(theme) { Glyph = FocusDraw.More, Quiet = true, AccessibleName = service.Text + " 계정 관리" };
        more.Click += (_, _) => { var menu = new ContextMenuStrip { Font = theme.Font() }; menu.Items.Add("계정 삭제…", null, (_, _) => delete()); menu.Closed += (_, _) => menu.Dispose(); menu.Show(more, new Point(0, more.Height)); };
        countdown = new Countdown(theme) { TabStop = false };
        if (featured) refresh = new Label { Text = "초 후 갱신", Font = theme.Font(8), ForeColor = theme.Muted, TextAlign = ContentAlignment.MiddleLeft };
        Controls.AddRange([service, name, code, copyButton, more, countdown]); if (refresh != null) Controls.Add(refresh);
    }
    private void CopyCode()
    {
        try { copy(account.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds())); }
        catch { MessageBox.Show("코드를 복사할 수 없습니다. 다시 시도하세요."); }
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e); if (service == null) return;
        if (Featured)
        {
            service.TextAlign = name.TextAlign = code.TextAlign = ContentAlignment.MiddleCenter;
            service.SetBounds(Px(24), Px(66), Width - Px(48), Px(23));
            name.SetBounds(Px(14), Px(90), Width - Px(28), Px(20));
            code.SetBounds(0, Px(116), Width, Px(52));
            int left = Math.Max(0, (Width - Px(190)) / 2);
            copyButton.SetBounds(left, Px(179), Px(92), Px(35)); countdown.SetBounds(left + Px(106), Px(182), Px(28), Px(28));
            refresh!.SetBounds(left + Px(138), Px(179), Px(70), Px(35)); more.SetBounds(Width - Px(25), Px(8), Px(25), Px(25));
        }
        else
        {
            service.SetBounds(0, Px(14), Width - Px(74), Px(20)); name.SetBounds(0, Px(36), Width - Px(56), Px(18));
            countdown.SetBounds(Width - Px(61), Px(18), Px(28), Px(28)); more.SetBounds(Width - Px(25), Px(19), Px(25), Px(25));
            code.SetBounds(0, Px(60), Width - Px(80), Px(36)); copyButton.SetBounds(Width - Px(70), Px(63), Px(70), Px(31));
        }
    }
    public void UpdateCode(long now)
    {
        try
        {
            string current = account.Code(now); string grouped = current.Insert(current.Length / 2, " ");
            if (code.Text != grouped) code.Text = grouped;
            countdown.UpdateTime(account.Period - (int)(now % account.Period), account.Period);
        }
        catch { code.Text = "코드 오류"; copyButton.Enabled = false; }
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
    protected override void Dispose(bool disposing) { if (disposing) code.Font.Dispose(); base.Dispose(disposing); }
}
