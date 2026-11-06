using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PadakKey.Core;
using ZXing;
using FormatException = System.FormatException;
namespace PadakKey;
internal sealed class Registration : Form
{
    public Account? Result { get; private set; }
    private readonly TextBox issuer = new() { PlaceholderText = "서비스 이름" }, name = new() { PlaceholderText = "계정 / 이메일" }, secret = new() { PlaceholderText = "Base32 인증 키", UseSystemPasswordChar = true }, uri = new() { PlaceholderText = "otpauth://totp/...", UseSystemPasswordChar = true };
    public Registration()
    {
        Text = "계정 추가"; ClientSize = new(420, 310); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new(16), WrapContents = false };
        foreach (var field in new[] { issuer, name, secret, uri }) { field.Width = 380; panel.Controls.Add(field); }
        var qr = new Button { Text = "QR 코드 이미지 열기", Width = 380, Height = 36 };
        qr.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            try { uri.Text = ReadQr(picker.FileName); _ = Otp.Parse(uri.Text); } catch (Exception ex) { MessageBox.Show(this, ex is FormatException ? ex.Message : "이미지를 읽을 수 없습니다."); }
        };
        panel.Controls.Add(qr);
        var save = new Button { Text = "계정 저장", Width = 380, Height = 36 };
        save.Click += (_, _) =>
        {
            try { Result = string.IsNullOrWhiteSpace(uri.Text) ? new Account(Guid.NewGuid(), issuer.Text.Trim(), name.Text.Trim(), secret.Text.Trim()) : Otp.Parse(uri.Text); Result.Validate(); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { MessageBox.Show(this, ex is FormatException ? ex.Message : "인증 키와 계정 설정을 확인하세요."); }
        };
        panel.Controls.Add(save); Controls.Add(panel); AcceptButton = save;
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
