using System;
using System.Windows;
using System.Windows.Media;
using ShotMind.Core.Models;

namespace ShotMind.Core.Services;

public static class CrosshairService
{
    public const int GridSize = 80;
    public const int GridCenter = GridSize / 2; // 40

    public const byte PixelEmpty = 0;
    public const byte PixelStroke = 1;
    public const byte PixelOutline = 2;

    public static string GetStyleDisplayName(CrosshairStyle style) => style switch
    {
        CrosshairStyle.Simple => "Mira simples",
        CrosshairStyle.Open => "Mira aberta",
        CrosshairStyle.Closed => "Mira fechada",
        CrosshairStyle.CenterDot => "Mira com ponto central",
        CrosshairStyle.LinesAndDot => "Mira com linhas e ponto",
        _ => "Mira simples"
    };

    public static string GetStyleDescription(CrosshairStyle style) => style switch
    {
        CrosshairStyle.Simple => "Aparência equilibrada semelhante à mira padrão clássica.",
        CrosshairStyle.Open => "Linhas separadas com vão central livre, sem ponto central.",
        CrosshairStyle.Closed => "Linhas contínuas cruzadas formando uma mira fechada.",
        CrosshairStyle.CenterDot => "Foco minimalista apenas no ponto central de precisão.",
        CrosshairStyle.LinesAndDot => "Linhas separadas combinadas com ponto central de precisão.",
        _ => string.Empty
    };

    /// <summary>
    /// Builds an 80x80 classification mask for the configured crosshair geometry.
    /// 0 = transparent, 1 = crosshair stroke/dot pixel, 2 = 1px outline pixel.
    /// </summary>
    public static byte[] BuildMask(AppSettings settings)
    {
        if (settings == null) settings = new AppSettings();

        int scaleLength = settings.CrosshairLength > 0 ? settings.CrosshairLength : 12;
        if (settings.CrosshairStyle == CrosshairStyle.Simple && settings.CursorSize is 14 or 18 or 24 && settings.CrosshairLength == 12)
        {
            scaleLength = settings.CursorSize switch
            {
                14 => 9,
                24 => 16,
                _ => 12
            };
        }

        return BuildMask(
            settings.CrosshairStyle,
            settings.CrosshairThickness,
            scaleLength,
            settings.CrosshairGap,
            settings.CrosshairDotSize,
            settings.CrosshairShowDot,
            settings.CrosshairOutline);
    }

    public static byte[] BuildMask(
        CrosshairStyle style,
        int thickness,
        int length,
        int gap,
        int dotSize,
        bool showDot,
        bool showOutline)
    {
        byte[] mask = new byte[GridSize * GridSize];

        thickness = Math.Clamp(thickness, 1, 6);
        length = Math.Clamp(length, 4, 28);
        gap = Math.Clamp(gap, 0, 14);
        dotSize = Math.Clamp(dotSize, 1, 10);

        int halfLow = (thickness - 1) / 2;
        int halfHigh = thickness / 2;
        int minBand = GridCenter - halfLow;
        int maxBand = GridCenter + halfHigh;

        void SetStroke(int x, int y)
        {
            if (x >= 1 && x < GridSize - 1 && y >= 1 && y < GridSize - 1)
            {
                mask[y * GridSize + x] = PixelStroke;
            }
        }

        bool drawLines = style != CrosshairStyle.CenterDot;
        bool drawCenterDot = style switch
        {
            CrosshairStyle.CenterDot => true,
            CrosshairStyle.LinesAndDot => true,
            CrosshairStyle.Open => showDot,
            CrosshairStyle.Closed => showDot,
            CrosshairStyle.Simple => showDot,
            _ => showDot
        };

        if (drawLines)
        {
            if (style == CrosshairStyle.Closed)
            {
                int totalArm = length + (gap / 2);
                int start = Math.Max(2, GridCenter - totalArm);
                int end = Math.Min(GridSize - 3, GridCenter + totalArm);

                // Vertical full line
                for (int y = start; y <= end; y++)
                {
                    for (int x = minBand; x <= maxBand; x++)
                    {
                        SetStroke(x, y);
                    }
                }

                // Horizontal full line
                for (int x = start; x <= end; x++)
                {
                    for (int y = minBand; y <= maxBand; y++)
                    {
                        SetStroke(x, y);
                    }
                }
            }
            else
            {
                int effectiveGap = style switch
                {
                    CrosshairStyle.Simple => Math.Max(3, gap),
                    CrosshairStyle.LinesAndDot => Math.Max(gap, (dotSize / 2) + 3),
                    _ => Math.Max(2, gap)
                };

                if (drawCenterDot)
                {
                    effectiveGap = Math.Max(effectiveGap, (dotSize / 2) + 2);
                }

                int topStart = Math.Max(2, GridCenter - effectiveGap - length);
                int topEnd = Math.Max(2, GridCenter - effectiveGap);

                int bottomStart = Math.Min(GridSize - 3, GridCenter + effectiveGap);
                int bottomEnd = Math.Min(GridSize - 3, GridCenter + effectiveGap + length);

                // Top & Bottom arms
                for (int y = topStart; y <= topEnd; y++)
                {
                    for (int x = minBand; x <= maxBand; x++)
                    {
                        SetStroke(x, y);
                    }
                }

                for (int y = bottomStart; y <= bottomEnd; y++)
                {
                    for (int x = minBand; x <= maxBand; x++)
                    {
                        SetStroke(x, y);
                    }
                }

                // Left & Right arms
                int leftStart = Math.Max(2, GridCenter - effectiveGap - length);
                int leftEnd = Math.Max(2, GridCenter - effectiveGap);

                int rightStart = Math.Min(GridSize - 3, GridCenter + effectiveGap);
                int rightEnd = Math.Min(GridSize - 3, GridCenter + effectiveGap + length);

                for (int x = leftStart; x <= leftEnd; x++)
                {
                    for (int y = minBand; y <= maxBand; y++)
                    {
                        SetStroke(x, y);
                    }
                }

                for (int x = rightStart; x <= rightEnd; x++)
                {
                    for (int y = minBand; y <= maxBand; y++)
                    {
                        SetStroke(x, y);
                    }
                }
            }
        }

        if (drawCenterDot)
        {
            int dLow = (dotSize - 1) / 2;
            int dHigh = dotSize / 2;

            if (dotSize <= 3)
            {
                for (int y = GridCenter - dLow; y <= GridCenter + dHigh; y++)
                {
                    for (int x = GridCenter - dLow; x <= GridCenter + dHigh; x++)
                    {
                        SetStroke(x, y);
                    }
                }
            }
            else
            {
                double centerCoord = GridCenter + (dotSize % 2 == 0 ? 0.5 : 0.0);
                double radius = dotSize / 2.0 + 0.15;
                double rSq = radius * radius;

                for (int y = GridCenter - dotSize; y <= GridCenter + dotSize; y++)
                {
                    for (int x = GridCenter - dotSize; x <= GridCenter + dotSize; x++)
                    {
                        double dx = x - centerCoord;
                        double dy = y - centerCoord;
                        if (dx * dx + dy * dy <= rSq)
                        {
                            SetStroke(x, y);
                        }
                    }
                }
            }
        }

        if (showOutline)
        {
            for (int y = 1; y < GridSize - 1; y++)
            {
                for (int x = 1; x < GridSize - 1; x++)
                {
                    if (mask[y * GridSize + x] != PixelEmpty) continue;

                    bool touchesStroke =
                        mask[(y - 1) * GridSize + x] == PixelStroke ||
                        mask[(y + 1) * GridSize + x] == PixelStroke ||
                        mask[y * GridSize + (x - 1)] == PixelStroke ||
                        mask[y * GridSize + (x + 1)] == PixelStroke ||
                        mask[(y - 1) * GridSize + (x - 1)] == PixelStroke ||
                        mask[(y - 1) * GridSize + (x + 1)] == PixelStroke ||
                        mask[(y + 1) * GridSize + (x - 1)] == PixelStroke ||
                        mask[(y + 1) * GridSize + (x + 1)] == PixelStroke;

                    if (touchesStroke)
                    {
                        mask[y * GridSize + x] = PixelOutline;
                    }
                }
            }
        }

        return mask;
    }

    /// <summary>
    /// Computes the inverted ARGB pixel for a given screen RGB value, ensuring strong contrast
    /// so white surfaces produce dark crosshair pixels and dark surfaces produce bright crosshair pixels.
    /// </summary>
    public static int InvertScreenPixel(int rawScreenArgb, bool isInsideSelection)
    {
        int r = (rawScreenArgb >> 16) & 0xFF;
        int g = (rawScreenArgb >> 8) & 0xFF;
        int b = rawScreenArgb & 0xFF;

        int rawLum = (int)(0.299 * r + 0.587 * g + 0.114 * b);

        // True color inversion of the underlying screen pixel
        int invR = 255 - r;
        int invG = 255 - g;
        int invB = 255 - b;

        // Effective displayed pixel on screen (outside selection has 50% black dimming mask)
        int dispR = isInsideSelection ? r : (r >> 1);
        int dispG = isInsideSelection ? g : (g >> 1);
        int dispB = isInsideSelection ? b : (b >> 1);

        int dispLum = (int)(0.299 * dispR + 0.587 * dispG + 0.114 * dispB);
        int invLum = (int)(0.299 * invR + 0.587 * invG + 0.114 * invB);

        // Ensure high visibility when the inverted color would be too close to the displayed background
        if (Math.Abs(invLum - dispLum) < 65)
        {
            if (rawLum >= 128)
            {
                // Underlying surface is light -> crosshair must be distinctly dark
                invR = Math.Max(0, Math.Min(invR, dispR - 85));
                invG = Math.Max(0, Math.Min(invG, dispG - 85));
                invB = Math.Max(0, Math.Min(invB, dispB - 85));
            }
            else
            {
                // Underlying surface is dark -> crosshair must be distinctly bright
                invR = Math.Min(255, Math.Max(invR, dispR + 95));
                invG = Math.Min(255, Math.Max(invG, dispG + 95));
                invB = Math.Min(255, Math.Max(invB, dispB + 95));
            }
        }

        return unchecked((int)((0xFF << 24) | (invR << 16) | (invG << 8) | invB));
    }

    /// <summary>
    /// Renders the crosshair into an 80x80 Bgra32 pixel buffer.
    /// In localized pixel inversion mode (AutoContrastCursor = true), only pixels directly covered
    /// by the crosshair strokes/dot (PixelStroke) are inverted against the underlying screen pixels.
    /// In fixed color mode (AutoContrastCursor = false), strokes use CursorColor and optional outline.
    /// </summary>
    public static void RenderToBuffer(
        int[] targetBuffer,
        byte[] mask,
        AppSettings settings,
        int[]? screenPixels,
        int screenWidth,
        int screenHeight,
        int cursorX,
        int cursorY,
        Rect selectionRect,
        bool hasSelection)
    {
        if (targetBuffer == null || targetBuffer.Length < GridSize * GridSize) return;
        Array.Clear(targetBuffer, 0, GridSize * GridSize);

        if (settings.AutoContrastCursor && screenPixels != null && screenWidth > 0 && screenHeight > 0)
        {
            for (int cy = 0; cy < GridSize; cy++)
            {
                int rowOffset = cy * GridSize;
                int sy = Math.Clamp(cursorY + (cy - GridCenter), 0, screenHeight - 1);
                int screenRowOffset = sy * screenWidth;

                for (int cx = 0; cx < GridSize; cx++)
                {
                    byte m = mask[rowOffset + cx];
                    if (m != PixelStroke) continue;

                    int sx = Math.Clamp(cursorX + (cx - GridCenter), 0, screenWidth - 1);
                    int raw = screenPixels[screenRowOffset + sx];
                    bool inside = hasSelection && selectionRect.Contains(sx, sy);

                    targetBuffer[rowOffset + cx] = InvertScreenPixel(raw, inside);
                }
            }
        }
        else
        {
            Color strokeColor = ParseColorOrDefault(settings.CursorColor, Color.FromRgb(0x8B, 0x5C, 0xF6));
            int strokeArgb = unchecked((int)((0xFF << 24) | (strokeColor.R << 16) | (strokeColor.G << 8) | strokeColor.B));

            double lum = (0.2126 * strokeColor.R + 0.7152 * strokeColor.G + 0.0722 * strokeColor.B) / 255.0;
            int outlineArgb = lum > 0.55
                ? unchecked((int)0xE6000000) // Dark outline for bright crosshair
                : unchecked((int)0xE6FFFFFF); // White outline for dark crosshair

            bool drawOutline = settings.CrosshairOutline;

            for (int i = 0; i < GridSize * GridSize; i++)
            {
                byte m = mask[i];
                if (m == PixelStroke)
                {
                    targetBuffer[i] = strokeArgb;
                }
                else if (m == PixelOutline && drawOutline)
                {
                    targetBuffer[i] = outlineArgb;
                }
            }
        }
    }

    public static Color ParseColorOrDefault(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try
        {
            string clean = hex.Trim();
            if (!clean.StartsWith('#')) clean = "#" + clean;
            if (ColorConverter.ConvertFromString(clean) is Color c)
            {
                return c;
            }
        }
        catch { }
        return fallback;
    }
}
