using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using ShotMind.Core.Models;

namespace ShotMind.Core.Services;

public static class ExportService
{
    public static string GenerateFileName(string pattern, ImageFormatType format, int? sequenceNumber = null)
    {
        return GenerateFileName(pattern, format == ImageFormatType.Png ? "png" : "jpg", sequenceNumber, DateTime.Now);
    }

    public static string GenerateFileName(string pattern, string ext, int? sequenceNumber = null, DateTime? date = null)
    {
        DateTime now = date ?? DateTime.Now;

        string result = string.IsNullOrWhiteSpace(pattern) ? "Captura_{yyyy-MM-dd_HH-mm-ss}" : pattern;

        // Backward compatibility
        result = result.Replace("{yyyy-MM-dd_HH-mm-ss}", now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));

        // Date and Time components
        result = result.Replace("{yyyy}", now.ToString("yyyy", CultureInfo.InvariantCulture))
                       .Replace("{YYYY}", now.ToString("yyyy", CultureInfo.InvariantCulture))
                       .Replace("{yy}", now.ToString("yy", CultureInfo.InvariantCulture))
                       .Replace("{YY}", now.ToString("yy", CultureInfo.InvariantCulture))
                       .Replace("{MM}", now.ToString("MM", CultureInfo.InvariantCulture))
                       .Replace("{dd}", now.ToString("dd", CultureInfo.InvariantCulture))
                       .Replace("{DD}", now.ToString("dd", CultureInfo.InvariantCulture))
                       .Replace("{HH}", now.ToString("HH", CultureInfo.InvariantCulture))
                       .Replace("{mm}", now.ToString("mm", CultureInfo.InvariantCulture))
                       .Replace("{ss}", now.ToString("ss", CultureInfo.InvariantCulture))
                       .Replace("{SS}", now.ToString("ss", CultureInfo.InvariantCulture))
                       .Replace("{data}", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                       .Replace("{date}", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                       .Replace("{hora}", now.ToString("HH-mm-ss", CultureInfo.InvariantCulture))
                       .Replace("{time}", now.ToString("HH-mm-ss", CultureInfo.InvariantCulture))
                       .Replace("{ano}", now.ToString("yyyy", CultureInfo.InvariantCulture))
                       .Replace("{mes}", now.ToString("MM", CultureInfo.InvariantCulture))
                       .Replace("{dia}", now.ToString("dd", CultureInfo.InvariantCulture))
                       .Replace("{minuto}", now.ToString("mm", CultureInfo.InvariantCulture))
                       .Replace("{segundo}", now.ToString("ss", CultureInfo.InvariantCulture));

        // Sequence number
        int seq = sequenceNumber ?? SettingsService.Instance.Current.SequenceCounter;
        int digits = Math.Clamp(SettingsService.Instance.Current.SequenceDigits, 1, 6);
        string seqStr = seq.ToString($"D{digits}");
        result = result.Replace("{seq}", seqStr)
                       .Replace("{SEQ}", seqStr)
                       .Replace("{num}", seqStr)
                       .Replace("{NUM}", seqStr)
                       .Replace("{numero}", seqStr)
                       .Replace("{sequencial}", seqStr);

        result = SanitizeFileName(result);

        if (!result.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase))
        {
            result += $".{ext}";
        }

        return result;
    }

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "Captura";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(fileName.Length);
        foreach (char c in fileName)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        string clean = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(clean) ? "Captura" : clean;
    }

    public static string GetUniqueFilePath(string directory, string desiredFileName)
    {
        string fullPath = Path.Combine(directory, desiredFileName);
        if (!File.Exists(fullPath))
        {
            return fullPath;
        }

        string nameWithoutExt = Path.GetFileNameWithoutExtension(desiredFileName);
        string ext = Path.GetExtension(desiredFileName);

        int counter = 1;
        while (true)
        {
            string candidate = Path.Combine(directory, $"{nameWithoutExt} ({counter}){ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
            counter++;
        }
    }

    public static Bitmap RenderAnnotatedBitmap(Bitmap baseBitmap, IEnumerable<AnnotationItem> annotations)
    {
        var result = new Bitmap(baseBitmap.Width, baseBitmap.Height, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(result))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. Draw base image
            g.DrawImage(baseBitmap, 0, 0, baseBitmap.Width, baseBitmap.Height);

            // 2. Draw annotations in order
            foreach (var item in annotations)
            {
                DrawAnnotation(g, result, item);
            }
        }

        return result;
    }

    private static void DrawAnnotation(Graphics g, Bitmap fullBitmap, AnnotationItem item)
    {
        var gdiColor = System.Drawing.Color.FromArgb(item.Color.A, item.Color.R, item.Color.G, item.Color.B);

        switch (item.Type)
        {
            case ToolType.Pen:
                if (item.Points.Count > 1)
                {
                    var penColor = item.IsPencil
                        ? System.Drawing.Color.FromArgb(170, gdiColor.R, gdiColor.G, gdiColor.B)
                        : gdiColor;
                    using var pen = new System.Drawing.Pen(penColor, (float)item.StrokeThickness)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round
                    };
                    for (int i = 0; i < item.Points.Count - 1; i++)
                    {
                        g.DrawLine(pen,
                            (float)item.Points[i].X, (float)item.Points[i].Y,
                            (float)item.Points[i + 1].X, (float)item.Points[i + 1].Y);
                    }
                }
                break;

            case ToolType.Highlighter:
                if (item.Points.Count > 1)
                {
                    // Semi-transparent with customizable opacity
                    byte alpha = item.HighlighterOpacity > 0 ? item.HighlighterOpacity : (byte)90;
                    var highlightColor = System.Drawing.Color.FromArgb(alpha, item.Color.R, item.Color.G, item.Color.B);
                    using var hlPen = new System.Drawing.Pen(highlightColor, (float)Math.Max(item.StrokeThickness * 3, 16))
                    {
                        StartCap = LineCap.Square,
                        EndCap = LineCap.Square
                    };
                    for (int i = 0; i < item.Points.Count - 1; i++)
                    {
                        g.DrawLine(hlPen,
                            (float)item.Points[i].X, (float)item.Points[i].Y,
                            (float)item.Points[i + 1].X, (float)item.Points[i + 1].Y);
                    }
                }
                break;

            case ToolType.Line:
                using (var linePen = new System.Drawing.Pen(gdiColor, (float)item.StrokeThickness)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                })
                {
                    g.DrawLine(linePen, (float)item.StartPoint.X, (float)item.StartPoint.Y, (float)item.EndPoint.X, (float)item.EndPoint.Y);
                }
                break;

            case ToolType.Arrow:
                using (var arrowPen = new System.Drawing.Pen(gdiColor, (float)item.StrokeThickness)
                {
                    StartCap = LineCap.Round,
                    CustomEndCap = new AdjustableArrowCap(5, 5, true)
                })
                {
                    g.DrawLine(arrowPen, (float)item.StartPoint.X, (float)item.StartPoint.Y, (float)item.EndPoint.X, (float)item.EndPoint.Y);
                }
                break;

            case ToolType.Rectangle:
                {
                    float x = (float)Math.Min(item.StartPoint.X, item.EndPoint.X);
                    float y = (float)Math.Min(item.StartPoint.Y, item.EndPoint.Y);
                    float w = (float)Math.Abs(item.StartPoint.X - item.EndPoint.X);
                    float h = (float)Math.Abs(item.StartPoint.Y - item.EndPoint.Y);

                    if (item.HasFill)
                    {
                        var fillColor = item.FillColor.A > 0
                            ? System.Drawing.Color.FromArgb(item.FillColor.A, item.FillColor.R, item.FillColor.G, item.FillColor.B)
                            : System.Drawing.Color.FromArgb(70, gdiColor.R, gdiColor.G, gdiColor.B);
                        using var fillBrush = new SolidBrush(fillColor);
                        g.FillRectangle(fillBrush, x, y, w, h);
                    }

                    using var rectPen = new System.Drawing.Pen(gdiColor, (float)item.StrokeThickness);
                    g.DrawRectangle(rectPen, x, y, w, h);
                }
                break;

            case ToolType.Ellipse:
                {
                    float x = (float)Math.Min(item.StartPoint.X, item.EndPoint.X);
                    float y = (float)Math.Min(item.StartPoint.Y, item.EndPoint.Y);
                    float w = (float)Math.Abs(item.StartPoint.X - item.EndPoint.X);
                    float h = (float)Math.Abs(item.StartPoint.Y - item.EndPoint.Y);

                    if (item.HasFill)
                    {
                        var fillColor = item.FillColor.A > 0
                            ? System.Drawing.Color.FromArgb(item.FillColor.A, item.FillColor.R, item.FillColor.G, item.FillColor.B)
                            : System.Drawing.Color.FromArgb(70, gdiColor.R, gdiColor.G, gdiColor.B);
                        using var fillBrush = new SolidBrush(fillColor);
                        g.FillEllipse(fillBrush, x, y, w, h);
                    }

                    using var ellipsePen = new System.Drawing.Pen(gdiColor, (float)item.StrokeThickness);
                    g.DrawEllipse(ellipsePen, x, y, w, h);
                }
                break;

            case ToolType.Text:
                if (!string.IsNullOrEmpty(item.Text))
                {
                    using var brush = new SolidBrush(gdiColor);
                    using var font = new Font("Segoe UI", (float)item.FontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
                    g.DrawString(item.Text, font, brush, (float)item.StartPoint.X, (float)item.StartPoint.Y);
                }
                break;

            case ToolType.StepMarker:
                {
                    float size = (float)(item.MarkerSize > 0 ? item.MarkerSize : 26.0);
                    float half = size / 2f;
                    float cx = (float)item.StartPoint.X;
                    float cy = (float)item.StartPoint.Y;
                    var circleRect = new RectangleF(cx - half, cy - half, size, size);

                    if (item.MarkerHasFill)
                    {
                        using var fillBrush = new SolidBrush(gdiColor);
                        g.FillEllipse(fillBrush, circleRect);
                    }

                    if (item.MarkerHasBorder)
                    {
                        using var borderPen = new System.Drawing.Pen(System.Drawing.Color.White, 1.5f);
                        g.DrawEllipse(borderPen, circleRect);
                    }

                    if (item.MarkerStyle != StepMarkerStyle.Dots && !string.IsNullOrEmpty(item.MarkerText))
                    {
                        var txtGdiColor = System.Drawing.Color.FromArgb(
                            item.MarkerTextColor.A > 0 ? item.MarkerTextColor.A : 255,
                            item.MarkerTextColor.R,
                            item.MarkerTextColor.G,
                            item.MarkerTextColor.B);
                        using var txtBrush = new SolidBrush(txtGdiColor);
                        float fontSize = Math.Max(9f, size * 0.46f);
                        using var font = new Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
                        using var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };
                        g.DrawString(item.MarkerText, font, txtBrush, circleRect, sf);
                    }
                }
                break;

            case ToolType.Blur:
                {
                    int x = (int)Math.Min(item.StartPoint.X, item.EndPoint.X);
                    int y = (int)Math.Min(item.StartPoint.Y, item.EndPoint.Y);
                    int w = (int)Math.Abs(item.StartPoint.X - item.EndPoint.X);
                    int h = (int)Math.Abs(item.StartPoint.Y - item.EndPoint.Y);

                    if (w > 2 && h > 2)
                    {
                        var rect = new Rectangle(x, y, w, h);
                        int intensity = item.ObfuscationIntensity > 0
                            ? item.ObfuscationIntensity
                            : SettingsService.Instance.Current.ObfuscationIntensity;
                        if (intensity <= 0) intensity = 12;

                        if (item.ObfuscationStyle == ObfuscationStyle.Blur)
                        {
                            ApplyBlur(fullBitmap, rect, intensity);
                        }
                        else
                        {
                            ApplyPixelate(fullBitmap, rect, intensity);
                        }
                    }
                }
                break;
        }
    }

    public static void ApplyPixelate(Bitmap image, Rectangle rect, int pixelSize)
    {
        rect.Intersect(new Rectangle(0, 0, image.Width, image.Height));
        if (rect.Width <= 1 || rect.Height <= 1) return;
        if (pixelSize <= 1) pixelSize = 12;

        var data = image.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                byte* scan0 = (byte*)data.Scan0;
                int stride = data.Stride;
                int w = rect.Width;
                int h = rect.Height;

                for (int y = 0; y < h; y += pixelSize)
                {
                    int blockH = Math.Min(pixelSize, h - y);
                    for (int x = 0; x < w; x += pixelSize)
                    {
                        int blockW = Math.Min(pixelSize, w - x);

                        long totalB = 0, totalG = 0, totalR = 0, totalA = 0;
                        int count = blockW * blockH;

                        for (int by = 0; by < blockH; by++)
                        {
                            byte* row = scan0 + (y + by) * stride + x * 4;
                            for (int bx = 0; bx < blockW; bx++)
                            {
                                totalB += row[bx * 4];
                                totalG += row[bx * 4 + 1];
                                totalR += row[bx * 4 + 2];
                                totalA += row[bx * 4 + 3];
                            }
                        }

                        byte avgB = (byte)(totalB / count);
                        byte avgG = (byte)(totalG / count);
                        byte avgR = (byte)(totalR / count);
                        byte avgA = (byte)(totalA / count);

                        for (int by = 0; by < blockH; by++)
                        {
                            byte* row = scan0 + (y + by) * stride + x * 4;
                            for (int bx = 0; bx < blockW; bx++)
                            {
                                row[bx * 4] = avgB;
                                row[bx * 4 + 1] = avgG;
                                row[bx * 4 + 2] = avgR;
                                row[bx * 4 + 3] = avgA;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            image.UnlockBits(data);
        }
    }

    public static void ApplyBlur(Bitmap image, Rectangle rect, int intensity)
    {
        rect.Intersect(new Rectangle(0, 0, image.Width, image.Height));
        if (rect.Width <= 1 || rect.Height <= 1) return;
        if (intensity <= 0) intensity = 12;

        int radius = Math.Clamp(intensity, 2, 40);
        BoxBlur(image, rect, radius);
    }

    private static void BoxBlur(Bitmap image, Rectangle rect, int radius)
    {
        var data = image.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                byte* scan0 = (byte*)data.Scan0;
                int stride = data.Stride;
                int w = rect.Width;
                int h = rect.Height;

                for (int pass = 0; pass < 2; pass++)
                {
                    // Horizontal pass
                    byte[] tempRow = new byte[w * 4];
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = scan0 + y * stride;
                        int div = radius * 2 + 1;
                        int sumB = 0, sumG = 0, sumR = 0, sumA = 0;

                        for (int i = -radius; i <= radius; i++)
                        {
                            int xi = Math.Clamp(i, 0, w - 1);
                            sumB += row[xi * 4];
                            sumG += row[xi * 4 + 1];
                            sumR += row[xi * 4 + 2];
                            sumA += row[xi * 4 + 3];
                        }

                        for (int x = 0; x < w; x++)
                        {
                            tempRow[x * 4] = (byte)(sumB / div);
                            tempRow[x * 4 + 1] = (byte)(sumG / div);
                            tempRow[x * 4 + 2] = (byte)(sumR / div);
                            tempRow[x * 4 + 3] = (byte)(sumA / div);

                            int xRemove = Math.Clamp(x - radius, 0, w - 1);
                            int xAdd = Math.Clamp(x + radius + 1, 0, w - 1);

                            sumB += row[xAdd * 4] - row[xRemove * 4];
                            sumG += row[xAdd * 4 + 1] - row[xRemove * 4 + 1];
                            sumR += row[xAdd * 4 + 2] - row[xRemove * 4 + 2];
                            sumA += row[xAdd * 4 + 3] - row[xRemove * 4 + 3];
                        }

                        for (int i = 0; i < w * 4; i++)
                        {
                            row[i] = tempRow[i];
                        }
                    }

                    // Vertical pass
                    byte[] tempCol = new byte[h * 4];
                    for (int x = 0; x < w; x++)
                    {
                        int div = radius * 2 + 1;
                        int sumB = 0, sumG = 0, sumR = 0, sumA = 0;

                        for (int i = -radius; i <= radius; i++)
                        {
                            int yi = Math.Clamp(i, 0, h - 1);
                            byte* pixel = scan0 + yi * stride + x * 4;
                            sumB += pixel[0];
                            sumG += pixel[1];
                            sumR += pixel[2];
                            sumA += pixel[3];
                        }

                        for (int y = 0; y < h; y++)
                        {
                            tempCol[y * 4] = (byte)(sumB / div);
                            tempCol[y * 4 + 1] = (byte)(sumG / div);
                            tempCol[y * 4 + 2] = (byte)(sumR / div);
                            tempCol[y * 4 + 3] = (byte)(sumA / div);

                            int yRemove = Math.Clamp(y - radius, 0, h - 1);
                            int yAdd = Math.Clamp(y + radius + 1, 0, h - 1);

                            byte* pAdd = scan0 + yAdd * stride + x * 4;
                            byte* pRem = scan0 + yRemove * stride + x * 4;

                            sumB += pAdd[0] - pRem[0];
                            sumG += pAdd[1] - pRem[1];
                            sumR += pAdd[2] - pRem[2];
                            sumA += pAdd[3] - pRem[3];
                        }

                        for (int y = 0; y < h; y++)
                        {
                            byte* pixel = scan0 + y * stride + x * 4;
                            pixel[0] = tempCol[y * 4];
                            pixel[1] = tempCol[y * 4 + 1];
                            pixel[2] = tempCol[y * 4 + 2];
                            pixel[3] = tempCol[y * 4 + 3];
                        }
                    }
                }
            }
        }
        finally
        {
            image.UnlockBits(data);
        }
    }

    public static System.Windows.Media.Imaging.BitmapSource? CreateObfuscatedRegionBitmap(
        Bitmap sourceBitmap,
        Rectangle cropRect,
        ObfuscationStyle style,
        int intensity)
    {
        try
        {
            var bounds = new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height);
            cropRect.Intersect(bounds);
            if (cropRect.Width <= 2 || cropRect.Height <= 2) return null;

            using var region = new Bitmap(cropRect.Width, cropRect.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(region))
            {
                g.DrawImage(sourceBitmap, new Rectangle(0, 0, cropRect.Width, cropRect.Height), cropRect, GraphicsUnit.Pixel);
            }

            if (style == ObfuscationStyle.Blur)
            {
                ApplyBlur(region, new Rectangle(0, 0, region.Width, region.Height), intensity);
            }
            else
            {
                ApplyPixelate(region, new Rectangle(0, 0, region.Width, region.Height), intensity);
            }

            return ScreenCaptureService.ToBitmapSource(region);
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao gerar preview de ofuscação.", ex);
            return null;
        }
    }

    public static string SaveImageToFile(Bitmap bitmap, string? filePath = null, ImageFormatType? format = null, int? quality = null)
    {
        var settings = SettingsService.Instance.Current;
        var chosenFormat = format ?? settings.DefaultFormat;
        int chosenQuality = quality ?? settings.JpgQuality;

        if (string.IsNullOrEmpty(filePath))
        {
            string dir = settings.DefaultSaveDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string name = GenerateFileName(settings.FilenamePattern, chosenFormat);
            filePath = GetUniqueFilePath(dir, name);
        }

        string? parent = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
        {
            Directory.CreateDirectory(parent);
        }

        if (chosenFormat == ImageFormatType.Png)
        {
            bitmap.Save(filePath, ImageFormat.Png);
        }
        else
        {
            var bytes = ScreenCaptureService.ToJpgByteArray(bitmap, chosenQuality);
            File.WriteAllBytes(filePath, bytes);
        }

        Logger.Info($"Imagem salva com sucesso em: {filePath}");
        return filePath;
    }

    public static void CopyToClipboard(Bitmap bitmap)
    {
        try
        {
            var bs = ScreenCaptureService.ToBitmapSource(bitmap);
            Clipboard.SetImage(bs);
            Logger.Info("Imagem copiada para a área de transferência.");
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao copiar imagem para área de transferência.", ex);
        }
    }
}
