using PadakKey.Core;
using System.Drawing.Imaging;

namespace PadakKey;

internal static class FocusChecks
{
    private static readonly Account[] Examples =
    [
        new(Guid.NewGuid(), "GitHub", "sm@example.com", "JBSWY3DPEHPK3PXP"),
        new(Guid.NewGuid(), "Google", "personal@gmail.com", "JBSWY3DPEHPK3PXP"),
        new(Guid.NewGuid(), "Microsoft", "work@outlook.com", "JBSWY3DPEHPK3PXP")
    ];
    public static void Run(string temporaryRoot, string? output = null)
    {
        string root = Path.Combine(temporaryRoot, "focus-ui"); var storage = new Storage(root); storage.Save(Examples.ToList());
        string icons = Path.Combine(root, "Icons"); Directory.CreateDirectory(icons);
        foreach (bool dark in new[] { false, true })
        {
            var theme = new FocusTheme(dark); string suffix = dark ? "dark" : "light";
            using var popup = new Popup(storage, _ => { }, () => { }, () => { }, theme: theme);
            popup.PreparePreview(); if (popup.VisibleAccountCount != 3) throw new Exception("Focus account list did not load");
            Capture(popup, output, "otp-" + suffix);
            using var settings = new FocusSettings(new Settings { Animation = false }, action => action(), _ => false, theme, icons);
            Capture(settings, output, "settings-" + suffix);
            using var registration = new Registration(theme); Capture(registration, output, "add-" + suffix);
        }
        using (var popup = new Popup(storage, _ => { }, () => { }, () => { }))
        {
            popup.PreparePreview(); int populatedHeight = popup.Height;
            storage.Save([]); popup.PreparePreview();
            if (popup.VisibleAccountCount != 0 || popup.Height >= populatedHeight) throw new Exception("Focus empty state did not resize");
            Capture(popup, output, "otp-empty");
            storage.Save([.. Examples, new(Guid.NewGuid(), "Long service name for an eight digit account", "long-account-name@example.com", "JBSWY3DPEHPK3PXP", "SHA256", 8, 60)]);
            popup.PreparePreview(); if (popup.VisibleAccountCount != 4) throw new Exception("Fourth account missing");
            Capture(popup, output, "otp-four-accounts");
        }
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "SparrowIdle.png"), Path.Combine(icons, "Example.png"));
        using (var settings = new FocusSettings(new Settings { Icon = "doze" }, action => action(), _ => false, iconFolder: icons)) Capture(settings, output, "settings-custom");
        using (var row = new OtpRow(new FocusTheme(false), Examples[0], true, _ => { }, () => { }))
        {
            row.Size = new Size(322, 228); row.UpdateCode(29);
            var buttons = row.Controls.OfType<OtpCodeButton>().ToArray();
            var current = buttons.Single(b => !b.IsNext); var next = buttons.Single(b => b.IsNext);
            if (current.Size != next.Size || current.Bounds.IntersectsWith(next.Bounds)) throw new Exception("OTP boxes must be equal and separate");
            if (!current.ClientRectangle.Contains(current.TimerBounds) || !next.TimerBounds.IsEmpty) throw new Exception("Countdown must be inside current box only");
            if (next.ForeColor != new FocusTheme(false).Muted || next.BackColor != new FocusTheme(false).Soft) throw new Exception("Next box must be gray");
            string before = string.Join(" ", row.Controls.OfType<OtpCodeButton>().Select(button => button.Text));
            row.UpdateCode(30); string after = string.Join(" ", row.Controls.OfType<OtpCodeButton>().Select(button => button.Text));
            if (before == after) throw new Exception("Displayed OTP did not refresh across a period boundary");
        }
        foreach (bool next in new[] { false, true })
        {
            Popup? copyPopup = null; bool copied = false;
            using var popup = new Popup(storage, _ => { copied = true; copyPopup!.Hide(); }, () => { }, () => { });
            copyPopup = popup; popup.PreparePreview(); popup.PreviewMode = true; popup.TopMost = false;
            popup.Location = new Point(-30000, -30000); popup.Show();
            var row = popup.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<OtpRow>().First();
            Button button = row.Controls.OfType<OtpCodeButton>().Single(button => button.IsNext == next);
            button.PerformClick();
            if (!copied || popup.Visible || !row.IsDisposed) throw new Exception("Current/next copy did not close and clear the popup");
        }
        long currentTime = 29; string? copiedNext = null;
        using (var row = new OtpRow(new FocusTheme(false), Examples[0], false, value => copiedNext = value, () => { }, () => currentTime))
        {
            using var form = new FocusForm { PreviewMode = true, TopMost = false, Location = new Point(-30000, -30000), ClientSize = new Size(322, 148) };
            row.Dock = DockStyle.Fill; form.Controls.Add(row); form.Show(); row.UpdateCode(currentTime);
            string expected = Examples[0].Code(30); if (row.Controls.OfType<OtpCodeButton>().Single(button => button.IsNext).Text.Replace(" ", "") != expected) throw new Exception("Next preview incorrect");
            currentTime = 30; row.Controls.OfType<OtpCodeButton>().Single(button => button.IsNext).PerformClick();
            if (copiedNext != Examples[0].Code(60)) throw new Exception("Next copy used a stale period");
            form.Hide();
        }
        storage.Save([Examples[0] with { Digits = 8, Period = 60 }]);
        using (var popup = new Popup(storage, _ => { }, () => { }, () => { })) { popup.PreparePreview(); Capture(popup, output, "otp-eight-digits"); }
        Console.WriteLine("PASS: Focus light/dark rendering, registration, icon settings/custom picker, empty/four-account layout, live current/next OTP, close after both copy actions");
    }
    private static void Capture(FocusForm form, string? output, string name)
    {
        form.PreviewMode = true; form.TopMost = false; form.Location = new Point(-30000, -30000); form.Show(); form.PerformLayout(); form.Update();
        using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, form.ClientRectangle);
        foreach (Control control in form.Controls)
        {
            if (control.Left < 0 || control.Top < 0 || control.Right > form.ClientSize.Width + 1 || control.Bottom > form.ClientSize.Height + 1)
                throw new Exception($"Control outside {name}: {control.GetType().Name} ({control.Bounds}, client {form.ClientSize})");
        }
        if (output != null) { Directory.CreateDirectory(output); image.Save(Path.Combine(output, name + ".png"), ImageFormat.Png); }
        form.Hide();
    }
    public static void Render(string output)
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "PadakKey-preview-" + Guid.NewGuid());
        try { Run(temporaryRoot, Path.GetFullPath(output)); }
        finally { if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }
    }
}
