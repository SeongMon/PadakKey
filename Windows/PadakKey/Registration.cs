using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PadakKey.Core;
using ZXing;
using FormatException = System.FormatException;
namespace PadakKey;
internal sealed class Registration : FocusForm
{
    public Account? Result { get; private set; }
    private readonly TextBox issuer = new() { PlaceholderText = "예: GitHub" }, name = new() { PlaceholderText = "name@example.com" }, secret = new() { PlaceholderText = "Base32 키 또는 otpauth://totp/...", UseSystemPasswordChar = true };
    public Registration(FocusTheme? theme = null) : base(theme)
    {
        Text = "OTP 추가"; ClientSize = new(370, 570); StartPosition = FormStartPosition.CenterScreen; MaximizeBox = false; MinimizeBox = false;
        var back = Button("", FocusDraw.Back); back.Quiet = true; back.AccessibleName = "OTP 추가 취소"; back.SetBounds(18, 16, 32, 32); back.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); }; Controls.Add(back);
        var title = Label("OTP 추가", 10); title.TextAlign = ContentAlignment.MiddleCenter; title.SetBounds(70, 16, 230, 32); Controls.Add(title);
        var emblem = Label(FocusDraw.Lock, 13); emblem.Font = new Font("Segoe MDL2 Assets", 14); emblem.BackColor = Theme.Soft; emblem.TextAlign = ContentAlignment.MiddleCenter; emblem.SetBounds(163, 78, 44, 44); Controls.Add(emblem);
        var heading = Label("새 계정을 연결하세요.", 14); heading.TextAlign = ContentAlignment.MiddleCenter; heading.SetBounds(24, 136, 322, 30); Controls.Add(heading);
        var hint = Label("서비스의 2단계 인증 설정에서\nQR 이미지나 인증 키를 가져오세요.", 8.5f); hint.ForeColor = Theme.Muted; hint.TextAlign = ContentAlignment.MiddleCenter; hint.SetBounds(24, 174, 322, 42); Controls.Add(hint);
        var manual = Button("인증 키 입력"); manual.Selected = true; manual.SetBounds(24, 233, 156, 34); Controls.Add(manual);
        var qr = Button("QR 이미지", FocusDraw.Image); qr.SetBounds(190, 233, 156, 34); Controls.Add(qr);
        var feedback = Label("", 8); feedback.ForeColor = Theme.Warning; feedback.SetBounds(24, 481, 322, 37); Controls.Add(feedback);
        void Field(string text, TextBox field, int y)
        {
            var label = Label(text, 8); label.ForeColor = Theme.Muted; label.SetBounds(24, y, 322, 19); Controls.Add(label);
            var frame = new Panel { Bounds = new Rectangle(24, y + 23, 322, 33), BackColor = Theme.Soft, Padding = new Padding(10, 8, 10, 6) };
            field.BorderStyle = BorderStyle.None; field.BackColor = Theme.Soft; field.ForeColor = Theme.Text; field.Font = Theme.Font(9); field.Dock = DockStyle.Fill; field.MaxLength = field == secret ? 8192 : 256; field.AccessibleName = text;
            frame.Controls.Add(field); Controls.Add(frame);
        }
        Field("서비스", issuer, 290); Field("계정", name, 353); Field("인증 키 또는 OTP URI", secret, 416);
        var save = Button("추가하기", "", true); save.SetBounds(24, 522, 322, 34); Controls.Add(save);
        manual.Click += (_, _) => { manual.Selected = true; qr.Selected = false; manual.Invalidate(); qr.Invalidate(); secret.Focus(); };
        qr.Click += async (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = "QR 이미지|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Title = "OTP QR 이미지 선택" };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            qr.Enabled = save.Enabled = false; feedback.Text = "QR 코드를 읽고 있어요…";
            try
            {
                string value = await Task.Run(() => ReadQr(picker.FileName)); var account = Otp.Parse(value);
                if (IsDisposed || Disposing) return;
                issuer.Text = account.Issuer; name.Text = account.Name; secret.Text = value;
                manual.Selected = false; qr.Selected = true; manual.Invalidate(); qr.Invalidate(); feedback.ForeColor = Theme.Accent; feedback.Text = "QR 코드를 읽었어요. 추가하기를 눌러 저장하세요.";
            }
            catch (Exception ex) { if (!IsDisposed && !Disposing) { feedback.ForeColor = Theme.Warning; feedback.Text = ex is FormatException ? ex.Message : "이미지를 읽을 수 없습니다."; } }
            finally { if (!IsDisposed && !Disposing) qr.Enabled = save.Enabled = true; }
        };
        save.Click += (_, _) =>
        {
            try { string value = secret.Text.Trim(); Result = value.StartsWith("otpauth:", StringComparison.OrdinalIgnoreCase) ? Otp.Parse(value) : new Account(Guid.NewGuid(), issuer.Text.Trim(), name.Text.Trim(), value); Result.Validate(); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { feedback.ForeColor = Theme.Warning; feedback.Text = ex is FormatException ? ex.Message : "인증 키와 계정 설정을 확인하세요."; }
        };
        AcceptButton = save; CancelButton = back;
        FormClosed += (_, _) => secret.Clear();
        Shown += (_, _) => { if (!PreviewMode) issuer.Focus(); };
    }
    internal static string ReadQr(string path)
    {
        if (new FileInfo(path).Length > 20 * 1024 * 1024) throw new FormatException("20MB 이하 이미지를 선택하세요.");
        using var original = new Bitmap(path);
        if ((long)original.Width * original.Height > 20_000_000) throw new FormatException("이미지 해상도가 너무 큽니다.");
        using var bitmap = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap)) { g.Clear(Color.White); g.DrawImageUnscaled(original, 0, 0); }
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (int y = 0; y < bitmap.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(data); }
        var reader = new BarcodeReaderGeneric { AutoRotate = true, Options = new ZXing.Common.DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
        var result = reader.Decode(new RGBLuminanceSource(pixels, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.BGRA32));
        return result?.Text ?? throw new FormatException("QR 코드를 찾지 못했습니다.");
    }
}
