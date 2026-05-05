using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace RemoteControl.Common
{
    public class ScreenCaptureResult
    {
        public byte[] ImageData { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ScreenLeft { get; set; }
        public int ScreenTop { get; set; }
    }

    public static class ScreenCapture
    {
        public static ScreenCaptureResult CaptureScreen(int quality = 80)
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                using (MemoryStream ms = new MemoryStream())
                {
                    ImageCodecInfo jpegEncoder = GetEncoder(ImageFormat.Jpeg);
                    EncoderParameters encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    bitmap.Save(ms, jpegEncoder, encoderParams);
                    return new ScreenCaptureResult
                    {
                        ImageData = ms.ToArray(),
                        Width = bitmap.Width,
                        Height = bitmap.Height,
                        ScreenLeft = bounds.Left,
                        ScreenTop = bounds.Top
                    };
                }
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }
    }
}
