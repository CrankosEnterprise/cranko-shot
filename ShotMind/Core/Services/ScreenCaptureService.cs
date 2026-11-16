using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace ShotMind.Core.Services;

public static class ScreenCaptureService
{
    public static Rectangle GetVirtualScreenBounds()
    {
        var screens = Screen.AllScreens;
        if (screens == null || screens.Length == 0)
        {
            return SystemInformation.VirtualScreen;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (var s in screens)
        {
            var b = s.Bounds;
            minX = Math.Min(minX, b.Left);
            minY = Math.Min(minY, b.Top);
            maxX = Math.Max(maxX, b.Right);
            maxY = Math.Max(maxY, b.Bottom);
        }

        // União com SystemInformation.VirtualScreen para garantir cobertura integral da área de trabalho
        var sysVirtual = SystemInformation.VirtualScreen;
        minX = Math.Min(minX, sysVirtual.Left);
        minY = Math.Min(minY, sysVirtual.Top);
        maxX = Math.Max(maxX, sysVirtual.Right);
        maxY = Math.Max(maxY, sysVirtual.Bottom);

        int totalWidth = Math.Max(1, maxX - minX);
        int totalHeight = Math.Max(1, maxY - minY);

        return new Rectangle(minX, minY, totalWidth, totalHeight);
    }

    public static Bitmap CaptureVirtualScreen()
    {
        var bounds = GetVirtualScreenBounds();
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.Black);

            var screens = Screen.AllScreens;
            if (screens != null && screens.Length > 1)
            {
                // Em configurações com múltiplos monitores (especialmente com diferentes resoluções,
                // orientações ou escalas de DPI), captura cada tela individualmente em suas coordenadas
                // físicas exatas para posicioná-la na grade global sem cortes ou distorções.
                foreach (var screen in screens)
                {
                    int destX = screen.Bounds.Left - bounds.Left;
                    int destY = screen.Bounds.Top - bounds.Top;
                    try
                    {
                        g.CopyFromScreen(
                            screen.Bounds.Left,
                            screen.Bounds.Top,
                            destX,
                            destY,
                            screen.Bounds.Size,
                            CopyPixelOperation.SourceCopy);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Falha ao copiar monitor individual '{screen.DeviceName}': {ex.Message}");
                    }
                }
            }
            else
            {
                // Monitor único ou fallback
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }
        }

        return bitmap;
    }

    public static Bitmap CropBitmap(Bitmap source, Rectangle cropRect)
    {
        // Clamp to source bounds
        int x = Math.Max(0, cropRect.X);
        int y = Math.Max(0, cropRect.Y);
        int width = Math.Min(source.Width - x, cropRect.Width);
        int height = Math.Min(source.Height - y, cropRect.Height);

        if (width <= 0 || height <= 0)
        {
            return new Bitmap(1, 1);
        }

        var target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(target))
        {
            g.DrawImage(source, new Rectangle(0, 0, width, height), new Rectangle(x, y, width, height), GraphicsUnit.Pixel);
        }
        return target;
    }

    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            var bitmapSource = BitmapSource.Create(
                bitmapData.Width,
                bitmapData.Height,
                96,
                96,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                bitmapData.Scan0,
                bitmapData.Stride * bitmapData.Height,
                bitmapData.Stride);

            bitmapSource.Freeze();
            return bitmapSource;
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    public static Bitmap ToGdiBitmap(BitmapSource bitmapSource)
    {
        int width = bitmapSource.PixelWidth;
        int height = bitmapSource.PixelHeight;
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            bitmapSource.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        return bmp;
    }

    public static byte[] ToPngByteArray(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public static byte[] ToJpgByteArray(Bitmap bitmap, int quality = 92)
    {
        using var ms = new MemoryStream();
        var encoder = GetEncoder(ImageFormat.Jpeg);
        if (encoder == null)
        {
            bitmap.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }

        var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
        bitmap.Save(ms, encoder, encoderParameters);
        return ms.ToArray();
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageEncoders();
        foreach (var codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        return null;
    }
}
