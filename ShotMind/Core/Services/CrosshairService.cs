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

    // Presets oficiais de escala rápida: cada um define um conjunto completo e independente de 4 dimensões
    public const int CursorSizeSmall = 14;
    public const int CursorSizeNormal = 18;
    public const int CursorSizeLarge = 24;
    public const int CursorSizeCustom = 0;

    public static readonly (int Thickness, int Length, int Gap, int DotSize) PresetSmall = (1, 8, 3, 2);
    public static readonly (int Thickness, int Length, int Gap, int DotSize) PresetNormal = (2, 12, 4, 3);
    public static readonly (int Thickness, int Length, int Gap, int DotSize) PresetLarge = (3, 18, 6, 5);

    public static bool MatchesPreset(int thickness, int length, int gap, int dotSize, (int Thickness, int Length, int Gap, int DotSize) preset)
    {
        return thickness == preset.Thickness &&
               length == preset.Length &&
               gap == preset.Gap &&
               dotSize == preset.DotSize;
    }

    public static int IdentifyPreset(int thickness, int length, int gap, int dotSize)
    {
        if (MatchesPreset(thickness, length, gap, dotSize, PresetSmall)) return CursorSizeSmall;
        if (MatchesPreset(thickness, length, gap, dotSize, PresetNormal)) return CursorSizeNormal;
        if (MatchesPreset(thickness, length, gap, dotSize, PresetLarge)) return CursorSizeLarge;
        return CursorSizeCustom;
    }

    public static string GetStyleDisplayName(CrosshairStyle style) => style switch
    {
        CrosshairStyle.Simple => "Mira simples",
        CrosshairStyle.Open => "Mira aberta",
        CrosshairStyle.Closed => "Mira fechada",
        CrosshairStyle.CenterDot => "Ponto central",
        CrosshairStyle.LinesAndDot => "Linhas + Ponto",
        _ => "Mira simples"
    };

    public static string GetStyleDescription(CrosshairStyle style) => style switch
    {
        CrosshairStyle.Simple => "Mira minimalista clássica contínua sem vão central.",
        CrosshairStyle.Open => "Quatro segmentos separados com vão central livre, sem linhas no centro.",
        CrosshairStyle.Closed => "Estrutura fechada com contorno delimitador conectado aos braços externos.",
        CrosshairStyle.CenterDot => "Ponto central de alta precisão exatamente no pixel do cursor.",
        CrosshairStyle.LinesAndDot => "Quatro segmentos separados combinados com ponto central de precisão.",
        _ => string.Empty
    };

    /// <summary>
    /// Maior distância (em pixels, a partir do centro) que as linhas podem ocupar dentro da grade 80x80.
    /// </summary>
    private const int MaxExtent = 37;

    /// <summary>
    /// Multiplicador informativo mantido para compatibilidade. As escalas rápidas são conjuntos
    /// predefinidos completos que atribuem valores diretamente a cada um dos quatro controles.
    /// </summary>
    public static double GetScaleMultiplier(int cursorSize) => cursorSize switch
    {
        CursorSizeSmall => 0.75,
        CursorSizeLarge => 1.5,
        _ => 1.0
    };

    /// <summary>
    /// Retorna a geometria efetiva (em pixels) diretamente a partir dos parâmetros configurados,
    /// garantindo consistência total entre os controles da interface, a prévia e a captura.
    /// </summary>
    public static (int Thickness, int Length, int Gap, int DotSize) GetScaledGeometry(AppSettings settings)
    {
        settings ??= new AppSettings();

        int thickness = Math.Clamp(settings.CrosshairThickness <= 0 ? 2 : settings.CrosshairThickness, 1, 6);
        int length = Math.Clamp(settings.CrosshairLength <= 0 ? 12 : settings.CrosshairLength, 4, 28);
        int gap = Math.Clamp(settings.CrosshairGap < 0 ? 4 : settings.CrosshairGap, 0, 14);
        int dot = Math.Clamp(settings.CrosshairDotSize <= 0 ? 3 : settings.CrosshairDotSize, 1, 10);

        return (thickness, length, gap, dot);
    }

    /// <summary>
    /// Builds an 80x80 classification mask for the configured crosshair geometry.
    /// 0 = transparent, 1 = crosshair stroke/dot pixel, 2 = 1px outline pixel.
    /// Utilizado identicamente pela prévia e pela mira real durante a captura.
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
    /// Todos os elementos são desenhados em torno de um único pivô no centro da grade 80x80
    /// (GridCenter = 40). O ponto central e os braços usam o alinhamento exato ao pivô, garantindo
    /// simetria perfeita e posicionamento pixel-perfect sobre o cursor.
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

        // Dentro da grade 80x80: se exceder o limite, reduz as dimensões na mesma proporção.
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

        // Pivô único no centro exato da grade:
        // Paridade da espessura quando há linhas; da paridade do ponto quando é só o ponto.
        bool evenPivot = drawLines ? thickness % 2 == 0 : dotSize % 2 == 0;
        double cc = GridCenter + (evenPivot ? 0.5 : 0.0);

        // O ponto tem a mesma paridade do pivô para garantir alinhamento exato com as linhas e o cursor
        int dotEff = dotSize;
        if (drawLines && (dotEff % 2 == 0) != evenPivot)
        {
            dotEff++;
        }

        double halfBand = (thickness - 1) / 2.0 + 1e-6;
        double dotHalfSquare = (dotEff - 1) / 2.0 + 1e-6;
        double dotRadius = dotEff / 2.0 + 0.15;
        double dotRadiusSq = dotRadius * dotRadius;

        // Vão central efetivo para mira aberta e linhas+ponto
        int effectiveGap = Math.Max(1, gap);
        if (drawCenterDot && drawLines && style == CrosshairStyle.LinesAndDot)
        {
            int dotRadiusK = (dotEff + 1) / 2;
            effectiveGap = Math.Max(gap, dotRadiusK + 1);
        }

        // Para a estrutura fechada (CrosshairStyle.Closed)
        int boxExtent = Math.Clamp(effectiveGap, 2, 14);
        int halfBandInt = (int)Math.Ceiling(halfBand);

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
                    switch (style)
                    {
                        case CrosshairStyle.Simple:
                            // Mira Simples: cruz minimalista pura contínua cruzando no centro geométrico sem gap
                            stroke = (inBandX && ky <= length) || (inBandY && kx <= length);
                            break;

                        case CrosshairStyle.Open:
                            // Mira Aberta: quatro segmentos separados com abertura central completamente vazia
                            stroke = (inBandX && ky >= effectiveGap && ky < effectiveGap + length)
                                  || (inBandY && kx >= effectiveGap && kx < effectiveGap + length);
                            break;

                        case CrosshairStyle.Closed:
                            // Mira Fechada: linhas conectadas formando uma estrutura fechada
                            // (quadrado central fechado de raio boxExtent + quatro braços externos conectados)
                            bool inBoxH = (Math.Abs(ky - boxExtent) <= halfBand) && (kx <= boxExtent + halfBandInt);
                            bool inBoxV = (Math.Abs(kx - boxExtent) <= halfBand) && (ky <= boxExtent + halfBandInt);
                            bool inArmOutY = inBandX && (ky >= boxExtent && ky < boxExtent + length);
                            bool inArmOutX = inBandY && (kx >= boxExtent && kx < boxExtent + length);
                            stroke = inBoxH || inBoxV || inArmOutX || inArmOutY;
                            break;

                        case CrosshairStyle.LinesAndDot:
                            // Linhas + Ponto: quatro segmentos separados com ponto de precisão central
                            stroke = (inBandX && ky >= effectiveGap && ky < effectiveGap + length)
                                  || (inBandY && kx >= effectiveGap && kx < effectiveGap + length);
                            break;
                    }
                }

                // Ponto central de precisão
                if (!stroke && drawCenterDot)
                {
                    stroke = dotEff <= 4
                        ? Math.Abs(dx) <= dotHalfSquare && Math.Abs(dy) <= dotHalfSquare
                        : (dx * dx + dy * dy) <= dotRadiusSq;
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
