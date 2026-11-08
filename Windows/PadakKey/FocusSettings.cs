using System.Diagnostics;

namespace PadakKey;

internal sealed class FocusSettings : FocusForm
{
    private readonly Settings settings;
    private readonly Action<Action> change;
    private readonly Func<IWin32Window, bool> import;
    private readonly List<FocusButton> choices = [];
    private readonly FocusToggle animation, startup;
    private readonly ComboBox size;
    private readonly FocusSlider zoom;
    private readonly Label zoomValue;
    private readonly PictureBox bird;
    private readonly string iconFolder;
    private bool loading, customSpace;

    public FocusSettings(Settings settings, Action<Action> change, Func<IWin32Window, bool> import, FocusTheme? theme = null, string? iconFolder = null) : base(theme)
    {
        this.settings = settings; this.change = change; this.import = import; this.iconFolder = iconFolder ?? Mascot.Folder;
        Text = "PadakKey 설정"; AccessibleName = "PadakKey 설정 및 아이콘 선택"; ClientSize = new(370, 568);
        var back = Button("", FocusDraw.Back); back.Quiet = true; back.AccessibleName = "설정 닫기"; back.SetBounds(18, 16, 32, 32); back.Click += (_, _) => Close(); Controls.Add(back);
        var title = Label("설정", 10); title.TextAlign = ContentAlignment.MiddleCenter; title.SetBounds(70, 16, 230, 32); Controls.Add(title);
        bird = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(318, 19, 28, 28), TabStop = false }; Controls.Add(bird);
        var caption = Label("아이콘 선택", 8); caption.ForeColor = Theme.Muted; caption.SetBounds(24, 73, 260, 20); Controls.Add(caption);
        AddChoice("파닥이는 참새", "flutter", 24); AddChoice("조는 참새", "doze", 190);

        AddLabel("애니메이션", 230); animation = new FocusToggle(Theme) { Bounds = new Rectangle(306, 224, 44, 32), AccessibleName = "참새 애니메이션" };
        animation.CheckedChanged += (_, _) => { if (!loading) Apply(() => settings.Animation = animation.Checked); }; Controls.Add(animation);
        AddLabel("트레이 아이콘 크기", 284);
        size = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Soft, ForeColor = Theme.Text, Font = Theme.Font(8), Bounds = new Rectangle(261, 280, 85, 28), AccessibleName = "트레이 아이콘 크기" };
        size.Items.AddRange(["60%", "75%", "90%", "100%"]); size.SelectedIndexChanged += (_, _) => { if (!loading) Apply(() => settings.IconSize = new[] { 60, 75, 90, 100 }[size.SelectedIndex]); }; Controls.Add(size);
        AddLabel("아이콘 확대", 336); zoomValue = Label("100%", 8); zoomValue.ForeColor = Theme.Muted; zoomValue.TextAlign = ContentAlignment.MiddleRight; zoomValue.SetBounds(275, 334, 71, 24); Controls.Add(zoomValue);
        zoom = new FocusSlider(Theme) { Bounds = new Rectangle(24, 368, 322, 27), AccessibleName = "아이콘 확대; 100에서 200퍼센트" };
        zoom.ValueChanged += (_, _) => zoomValue.Text = zoom.Value + "%";
        zoom.MouseUp += (_, _) => CommitZoom(); zoom.KeyUp += (_, _) => CommitZoom(); Controls.Add(zoom);
        foreach (var (text, x, width, alignment) in new[] { ("100%", 24, 70, ContentAlignment.MiddleLeft), ("여백 줄이기", 94, 182, ContentAlignment.MiddleCenter), ("200%", 276, 70, ContentAlignment.MiddleRight) })
        { var scale = Label(text, 7); scale.ForeColor = Theme.Muted; scale.TextAlign = alignment; scale.SetBounds(x, 400, width, 20); Controls.Add(scale); }

        var add = Button("아이콘 추가 · PNG / GIF", FocusDraw.Image); add.Quiet = true; add.SetBounds(24, 440, 322, 34);
        add.Click += (_, _) => { if (this.import(this)) { Reload(); RefreshCustoms(); } }; Controls.Add(add);
        var folder = Button("아이콘 폴더 열기", FocusDraw.Folder); folder.Quiet = true; folder.SetBounds(24, 480, 322, 34);
        folder.Click += (_, _) => { try { Directory.CreateDirectory(this.iconFolder); Process.Start(new ProcessStartInfo(this.iconFolder) { UseShellExecute = true }); } catch { MessageBox.Show(this, "아이콘 폴더를 열 수 없습니다."); } }; Controls.Add(folder);
        AddLabel("Windows 시작 시 실행", 532); startup = new FocusToggle(Theme) { Bounds = new Rectangle(306, 526, 44, 32), AccessibleName = "Windows 시작 시 실행" };
        startup.CheckedChanged += (_, _) =>
        {
            if (loading) return;
            try { StartupRegistration.Set(startup.Checked); }
            catch { MessageBox.Show(this, "자동 실행 설정을 저장할 수 없습니다."); loading = true; startup.Checked = StartupRegistration.Enabled; loading = false; }
        }; Controls.Add(startup);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Reload(); RefreshCustoms();
    }
    private void AddChoice(string text, string id, int x)
    {
        var button = Button(text); button.AccessibleName = text; button.Tag = id; button.SetBounds(x, 103, 156, 105);
        button.Preview = FocusDraw.Sparrow(id, 96, true); button.Click += (_, _) => { Apply(() => settings.Icon = id); Reload(); };
        choices.Add(button); Controls.Add(button);
    }
    private void AddLabel(string text, int y) { var label = Label(text, 9); label.SetBounds(24, y, 220, 26); Controls.Add(label); }
    private void Apply(Action action) { change(action); Reload(); }
    private void CommitZoom()
    {
        int rounded = Math.Clamp((int)Math.Round(zoom.Value / 25.0) * 25, 100, 200);
        Apply(() => settings.IconZoom = rounded);
    }
    private void Reload()
    {
        loading = true;
        animation.Checked = settings.Animation; startup.Checked = StartupRegistration.Enabled;
        size.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { 60, 75, 90, 100 }, Math.Clamp(settings.IconSize, 60, 100)));
        zoom.Value = Math.Clamp(settings.IconZoom, 100, 200); zoomValue.Text = zoom.Value + "%";
        foreach (var choice in choices) { choice.Selected = (string?)choice.Tag == settings.Icon; choice.Invalidate(); }
        bird.Image?.Dispose(); bird.Image = FocusDraw.Sparrow(settings.Icon, Px(28)); loading = false;
    }
    private void RefreshCustoms()
    {
        var existing = Controls.Find("custom-icons", false).FirstOrDefault();
        if (existing != null) { Controls.Remove(existing); existing.Dispose(); }
        var files = Directory.Exists(iconFolder) ? Directory.GetFiles(iconFolder).Where(p => new[] { ".png", ".gif" }.Contains(Path.GetExtension(p).ToLowerInvariant())).OrderBy(Path.GetFileName).ToArray() : [];
        bool needsSpace = files.Length > 0;
        if (needsSpace != customSpace)
        {
            int offset = Px(needsSpace ? 44 : -44);
            foreach (Control control in Controls) if (control.Top >= Px(224)) control.Top += offset;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + offset); customSpace = needsSpace;
        }
        if (files.Length == 0) return;
        var select = new ComboBox { Name = "custom-icons", DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Soft, ForeColor = Theme.Text, Font = Theme.Font(8), AccessibleName = "사용자 아이콘 선택" };
        select.Items.Add("사용자 아이콘 선택…"); foreach (var file in files) select.Items.Add(Path.GetFileName(file));
        int selected = Array.FindIndex(files, p => Path.GetFileName(p) == settings.Icon); select.SelectedIndex = selected + 1;
        select.SetBounds(Px(24), Px(221), Px(322), Px(26));
        select.SelectedIndexChanged += (_, _) => { if (select.SelectedIndex > 0) Apply(() => settings.Icon = Path.GetFileName(files[select.SelectedIndex - 1])); };
        Controls.Add(select); select.BringToFront();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); using var pen = new Pen(Theme.Line);
        foreach (int y in new[] { 266, 319, 429, 519 }) e.Graphics.DrawLine(pen, Px(24), Px(y + (customSpace ? 44 : 0)), ClientSize.Width - Px(24), Px(y + (customSpace ? 44 : 0)));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { bird.Image?.Dispose(); foreach (var choice in choices) choice.Preview?.Dispose(); }
        base.Dispose(disposing);
    }
}
