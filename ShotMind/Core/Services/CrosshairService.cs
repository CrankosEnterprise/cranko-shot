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
    /// Maior distância (em pixels, a partir do centro) que as linhas podem ocupar dentro da grade 80x80.
    /// </summary>
    private const int MaxExtent = 37;

    /// <summary>
    /// Multiplicador proporcional da "Escala rápida" (valor persistido em <see cref="AppSettings.CursorSize"/>).
    /// </summary>
    public static double GetScaleMultiplier(int cursorSize) => cursorSize switch
    {
        14 => 0.75,
        24 => 1.5,
        _ => 1.0
    };

    private static int ScaleValue(int value, double multiplier, int min)
        => Math.Max(min, (int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Geometria efetiva (em pixels) após aplicar a escala rápida UMA única vez sobre todos os
    /// elementos: espessura, comprimento, abertura central e tamanho do ponto.
    /// </summary>
    public static (int Thickness, int Length, int Gap, int DotSize) GetScaledGeometry(AppSettings settings)
    {
        settings ??= new AppSettings();
        double m = GetScaleMultiplier(settings.CursorSize);

        int thickness = Math.Clamp(settings.CrosshairThickness <= 0 ? 2 : settings.CrosshairThickness, 1, 6);
        int length = Math.Clamp(settings.CrosshairLength <= 0 ? 12 : settings.CrosshairLength, 4, 28);
        int gap = Math.Clamp(settings.CrosshairGap < 0 ? 4 : settings.CrosshairGap, 0, 14);
        int dot = Math.Clamp(settings.CrosshairDotSize <= 0 ? 3 : settings.CrosshairDotSize, 1, 10);

        return (
            ScaleValue(thickness, m, 1),
            ScaleValue(length, m, 1),
            ScaleValue(gap, m, gap > 0 ? 1 : 0),
            ScaleValue(dot, m, 1));
    }

    /// <summary>
    /// Builds an 80x80 classification mask for the configured crosshair geometry.
    /// 0 = transparent, 1 = crosshair stroke/dot pixel, 2 = 1px outline pixel.
    /// A escala rápida (CursorSize) é aplicada aqui, uma única vez, para a prévia e para a mira real.
    /// </summary>
    public static byte[] BuildMask(AppSettings settings)
    {
        if (settings == null) settings = new AppSettings();

        var g = GetScaledGeometry(settings);

        return BuildMask(
            settings.CrosshairStyle,
            g.Thickness,
            g.Length,
            g.Gap,
            g.DotSize,
            settings.CrosshairShowDot,
            settings.CrosshairOutline);
    }

    /// <summary>
    /// Todos os elementos são desenhados em torno de um único pivô (o pixel central da grade quando a
    /// espessura é ímpar, ou o par de pixels centrais quando é par). O ponto central adota a mesma
    /// paridade das linhas, e os braços usam a distância simétrica ao pivô, garantindo simetria
    /// perfeita e centralização exata.
    /// </summary>
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

        thickness = Math.Clamp(thickness, 1, 12);
        length = Math.Clamp(length, 1, 60);
        gap = Math.Clamp(gap, 0, 60);
        dotSize = Math.Clamp(dotSize, 1, 20);

        // Dentro da grade 80x80: se exceder o limite, reduz TODAS as dimensões na mesma proporção.
        if (gap + length > MaxExtent)
        {
            double f = MaxExtent / (double)(gap + length);
            length = Math.Max(1, (int)Math.Round(length * f, MidpointRounding.AwayFromZero));
            gap = (int)Math.Round(gap * f, MidpointRounding.AwayFromZero);
            thickness = Math.Max(1, (int)Math.Round(thickness * f, MidpointRounding.AwayFromZero));
            dotSize = Math.Max(1, (int)Math.Round(dotSize * f, MidpointRounding.AwayFromZero));
        }

        bool drawLines = style != CrosshairStyle.CenterDot;
        bool drawCenterDot = style switch
        {
            CrosshairStyle.CenterDot => true,
            CrosshairStyle.LinesAndDot => true,
            _ => showDot
        };

        // Pivô único: paridade da espessura quando há linhas; do próprio ponto quando é só o ponto.
        bool evenPivot = drawLines ? thickness % 2 == 0 : dotSize % 2 == 0;
        double cc = GridCenter + (evenPivot ? 0.5 : 0.0);

        // O ponto precisa ter a mesma paridade do pivô para ficar exatamente centralizado nas linhas.
        int dotEff = dotSize;
        if (drawLines && (dotEff % 2 == 0) != evenPivot)
        {
            dotEff++;
        }

        // Com ponto central, as linhas começam depois dele, deixando ao menos 1 pixel livre.
        int effectiveGap = gap;
        if (drawCenterDot && drawLines && style != CrosshairStyle.Closed)
        {
            int dotMaxK = ((dotEff + 1) / 2) - 1;
            effectiveGap = Math.Max(gap, dotMaxK + 2);
        }

        double halfBand = (thickness - 1) / 2.0 + 1e-6;
        int closedExtentK = effectiveGap + length - 1;
        double dotHalfSquare = (dotEff - 1) / 2.0 + 1e-6;
        double dotRadius = dotEff / 2.0 + 0.15;
        double dotRadiusSq = dotRadius * dotRadius;

        for (int y = 1; y < GridSize - 1; y++)
        {
            double dy = y - cc;
            int ky = (int)Math.Floor(Math.Abs(dy));
            bool inBandY = Math.Abs(dy) <= halfBand;

            for (int x = 1; x < GridSize - 1; x++)
            {
                double dx = x - cc;
                int kx = (int)Math.Floor(Math.Abs(dx));
                bool inBandX = Math.Abs(dx) <= halfBand;

                bool stroke = false;

                if (drawLines)
                {
                    if (style == CrosshairStyle.Closed)
                    {
                        stroke = (inBandX && ky <= closedExtentK) || (inBandY && kx <= closedExtentK);
                    }
                    else
                    {
                        stroke = (inBandX && ky >= effectiveGap && ky < effectiveGap + length)
                              || (inBandY && kx >= effectiveGap && kx < effectiveGap + length);
                    }
                }

                if (!stroke && drawCenterDot)
                {
                    stroke = dotEff <= 3
                        ? Math.Abs(dx) <= dotHalfSquare && Math.Abs(dy) <= dotHalfSquare
                        : dx * dx + dy * dy <= dotRadiusSq;
                }

                if (stroke)
                {
                    mask[y * GridSize + x] = PixelStroke;
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
