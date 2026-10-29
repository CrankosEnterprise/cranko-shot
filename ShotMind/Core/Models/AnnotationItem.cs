using System.Collections.Generic;
using System.Windows.Media;

namespace ShotMind.Core.Models;

public class AnnotationItem
{
    public ToolType Type { get; set; }
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
    public List<Point> Points { get; set; } = new();
    public Color Color { get; set; } = Colors.Red;
    public double StrokeThickness { get; set; } = 3.0;
    public string Text { get; set; } = string.Empty;
    public double FontSize { get; set; } = 16.0;
    public ObfuscationStyle ObfuscationStyle { get; set; } = ObfuscationStyle.Pixelate;
    public int ObfuscationIntensity { get; set; } = 12;

    // Fill Properties for Geometric Shapes
    public bool HasFill { get; set; } = false;
    public Color FillColor { get; set; } = Colors.Transparent;

    // Pen / Pencil Mode
    public bool IsPencil { get; set; } = false;

    // Highlighter Opacity
    public byte HighlighterOpacity { get; set; } = 90;

    // StepMarker Properties
    public StepMarkerStyle MarkerStyle { get; set; } = StepMarkerStyle.Numbers;
    public int MarkerNumber { get; set; } = 1;
    public string MarkerText { get; set; } = "1";
    public Color MarkerTextColor { get; set; } = Colors.White;
    public double MarkerSize { get; set; } = 26.0;
    public bool MarkerHasFill { get; set; } = true;
    public bool MarkerHasBorder { get; set; } = true;

    public AnnotationItem Clone()
    {
        return new AnnotationItem
        {
            Type = Type,
            StartPoint = StartPoint,
            EndPoint = EndPoint,
            Points = new List<Point>(Points),
            Color = Color,
            StrokeThickness = StrokeThickness,
            Text = Text,
            FontSize = FontSize,
            ObfuscationStyle = ObfuscationStyle,
            ObfuscationIntensity = ObfuscationIntensity,
            HasFill = HasFill,
            FillColor = FillColor,
            IsPencil = IsPencil,
            HighlighterOpacity = HighlighterOpacity,
            MarkerStyle = MarkerStyle,
            MarkerNumber = MarkerNumber,
            MarkerText = MarkerText,
            MarkerTextColor = MarkerTextColor,
            MarkerSize = MarkerSize,
            MarkerHasFill = MarkerHasFill,
            MarkerHasBorder = MarkerHasBorder
        };
    }

    public static string FormatMarkerText(StepMarkerStyle style, int number)
    {
        return style switch
        {
            StepMarkerStyle.Numbers => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StepMarkerStyle.Uppercase => ToAlphabetString(number, uppercase: true),
            StepMarkerStyle.Lowercase => ToAlphabetString(number, uppercase: false),
            StepMarkerStyle.Dots => string.Empty,
            _ => number.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static string ToAlphabetString(int number, bool uppercase)
    {
        if (number <= 0) number = 1;
        string result = string.Empty;
        while (number > 0)
        {
            number--;
            char c = (char)((uppercase ? 'A' : 'a') + (number % 26));
            result = c + result;
            number /= 26;
        }
        return result;
    }
}
