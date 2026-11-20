using PadakKey.Core;
namespace PadakKey;
internal static class WindowsChecks
{
    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "PadakKey-check-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var store = new Storage(root);
            var a = Otp.Parse("otpauth://totp/Test:alice?secret=JBSWY3DPEHPK3PXP&issuer=Test");
            store.Save([a]); if(store.Load().Single()!=a) throw new Exception("DPAPI roundtrip failed");
            var bytes=File.ReadAllBytes(Path.Combine(root,"accounts.dpapi"));
            if(System.Text.Encoding.UTF8.GetString(bytes).Contains(a.Secret))throw new Exception("Plaintext secret persisted");
            File.WriteAllBytes(Path.Combine(root,"accounts.dpapi"), [1,2,3]);
            bool failed=false;try{store.Load();}catch{failed=true;}if(!failed)throw new Exception("Corrupt vault accepted");
            foreach(var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"Assets"),"*.png")) { var frames=Mascot.ReadCustom(file);if(frames.Count!=1)throw new Exception("PNG loading failed");foreach(var f in frames)f.Image.Dispose(); }
            var matrix=new ZXing.BarcodeWriterPixelData { Format=ZXing.BarcodeFormat.QR_CODE, Options=new ZXing.Common.EncodingOptions{Width=300,Height=300,Margin=2} }.Write("otpauth://totp/Test:alice?secret=JBSWY3DPEHPK3PXP&issuer=Test");
            using var bmp=new Bitmap(matrix.Width,matrix.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data=bmp.LockBits(new Rectangle(0,0,bmp.Width,bmp.Height),System.Drawing.Imaging.ImageLockMode.WriteOnly,bmp.PixelFormat);
            try{System.Runtime.InteropServices.Marshal.Copy(matrix.Pixels,0,data.Scan0,matrix.Pixels.Length);}finally{bmp.UnlockBits(data);}
            string qr=Path.Combine(root,"qr.png");bmp.Save(qr);if(Otp.Parse(Registration.ReadQr(qr)).Name!="alice")throw new Exception("QR failed");
            using var tray = new NotifyIcon();var settings=new Settings{Animation=false};using var mascot=new Mascot(tray,settings);if(tray.Icon==null)throw new Exception("Missing tray icon");settings.Icon="doze";settings.IconSize=60;mascot.Reload();if(tray.Icon==null)throw new Exception("Doze failed");
            if(Math.Abs(Mascot.Remaining(DateTimeOffset.FromUnixTimeSeconds(60))-30)>0.001||Math.Abs(Mascot.Remaining(DateTimeOffset.FromUnixTimeMilliseconds(89_500))-0.5)>0.001)throw new Exception("Countdown window misaligned");
            using(var ring=new Bitmap(32,32)){using var on=mascot.Compose(ring,DateTimeOffset.FromUnixTimeSeconds(70));using var onBmp=on.ToBitmap();if(onBmp.GetPixel(16,1).A==0)throw new Exception("Countdown ring missing");
            settings.Countdown=false;mascot.Reload();using var off=mascot.Compose(ring,DateTimeOffset.FromUnixTimeSeconds(70));using var offBmp=off.ToBitmap();if(offBmp.GetPixel(16,1).A!=0)throw new Exception("Countdown ring drawn while off");settings.Countdown=true;mascot.Reload();}
            mascot.FlashCopied();mascot.Paused=true;mascot.Paused=false;
            FocusChecks.Run(root);
            Console.WriteLine("PASS: DPAPI roundtrip/corruption, PNG frames, QR image decoding, builtin icon loading, countdown ring");
        }
        finally{Directory.Delete(root,true);}
    }
}
