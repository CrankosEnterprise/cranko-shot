using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using ShotMind.Core;
using ShotMind.Core.Models;
using ShotMind.Core.Services;

namespace ShotMind.UI.Views;

public partial class FullEditorWindow : Window
{
    private Bitmap? _currentBitmap;
    private string? _currentFilePath;
    private bool _hasUnsavedChanges = false;

    private ToolType _currentTool = ToolType.Pen;
    private Color _currentColor = (Color)ColorConverter.ConvertFromString("#8B5CF6");

    // Per-tool customization options
    private bool _penIsPencil = false;
    private double _penThickness = 3.0;

    private double _highlighterThickness = 20.0;
    private byte _highlighterOpacity = 90; // ~35%

    private double _lineThickness = 3.0;
    private double _arrowThickness = 3.0;

    private double _rectThickness = 3.0;
    private string _rectFillMode = "None"; // None, Translucent, Solid

    private double _ellipseThickness = 3.0;
    private string _ellipseFillMode = "None"; // None, Translucent, Solid

    private double _textFontSize = 18.0;

    private ObfuscationStyle _blurStyle = ObfuscationStyle.Blur;
    private int _blurIntensity = 12;

    private StepMarkerStyle _markerStyle = StepMarkerStyle.Numbers;
    private double _markerSize = 26.0;

    // Flyout timer management
    private readonly System.Windows.Threading.DispatcherTimer _flyoutCloseTimer;
    private System.Windows.Controls.Primitives.Popup? _activeFlyout;

    private int _nextStepMarkerIndex = 1;
    private bool _isDrawing = false;
    private Point _startPoint;
    private AnnotationItem? _currentAnnotation;
    private FrameworkElement? _tempPreviewShape;

    private readonly List<AnnotationItem> _annotations = new();
    private readonly Stack<List<AnnotationItem>> _undoStack = new();
    private readonly Stack<List<AnnotationItem>> _redoStack = new();

    public FullEditorWindow()
    {
        InitializeComponent();

        _flyoutCloseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _flyoutCloseTimer.Tick += (s, e) =>
        {
            _flyoutCloseTimer.Stop();
            CloseAllFlyouts();
        };

        UpdateToolSelection();
        UpdateColorSelection();
        UpdateActiveOptionButtons();
    }

    private void UpdateActiveOptionButtons()
    {
        if (BtnEditorPenNormal != null) BtnEditorPenNormal.Tag = _penIsPencil ? "" : "Active";
        if (BtnEditorPenPencil != null) BtnEditorPenPencil.Tag = _penIsPencil ? "Active" : "";

        if (BtnRectFillNone != null) BtnRectFillNone.Tag = _rectFillMode == "None" ? "Active" : "";
        if (BtnRectFillTranslucent != null) BtnRectFillTranslucent.Tag = _rectFillMode == "Translucent" ? "Active" : "";
        if (BtnRectFillSolid != null) BtnRectFillSolid.Tag = _rectFillMode == "Solid" ? "Active" : "";

        if (BtnEllipseFillNone != null) BtnEllipseFillNone.Tag = _ellipseFillMode == "None" ? "Active" : "";
        if (BtnEllipseFillTranslucent != null) BtnEllipseFillTranslucent.Tag = _ellipseFillMode == "Translucent" ? "Active" : "";
        if (BtnEllipseFillSolid != null) BtnEllipseFillSolid.Tag = _ellipseFillMode == "Solid" ? "Active" : "";

        if (BtnEditorBlurMode != null) BtnEditorBlurMode.Tag = _blurStyle == ObfuscationStyle.Blur ? "Active" : "";
        if (BtnEditorPixelateMode != null) BtnEditorPixelateMode.Tag = _blurStyle == ObfuscationStyle.Pixelate ? "Active" : "";

        if (BtnEditorMarkerNumbers != null) BtnEditorMarkerNumbers.Tag = _markerStyle == StepMarkerStyle.Numbers ? "Active" : "";
        if (BtnEditorMarkerUpper != null) BtnEditorMarkerUpper.Tag = _markerStyle == StepMarkerStyle.Uppercase ? "Active" : "";
        if (BtnEditorMarkerLower != null) BtnEditorMarkerLower.Tag = _markerStyle == StepMarkerStyle.Lowercase ? "Active" : "";
        if (BtnEditorMarkerDots != null) BtnEditorMarkerDots.Tag = _markerStyle == StepMarkerStyle.Dots ? "Active" : "";
    }

    public void LoadBitmap(Bitmap bitmap, string? filePath = null)
    {
        _currentBitmap?.Dispose();
        _currentBitmap = new Bitmap(bitmap);
        _currentFilePath = filePath;
        _hasUnsavedChanges = false;

        DisplayImage.Source = ScreenCaptureService.ToBitmapSource(_currentBitmap);
        DrawingContainer.Width = _currentBitmap.Width;
        DrawingContainer.Height = _currentBitmap.Height;
        EditorAnnotationCanvas.Width = _currentBitmap.Width;
        EditorAnnotationCanvas.Height = _currentBitmap.Height;

        _annotations.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        _nextStepMarkerIndex = 1;
        EditorAnnotationCanvas.Children.Clear();

        StatusDimensions.Text = $"Dimensões: {_currentBitmap.Width} × {_currentBitmap.Height} px";
        StatusInfo.Text = string.IsNullOrEmpty(filePath) ? "Nova Captura" : Path.GetFileName(filePath);
    }

    #region File Operations (Open, Save, Save As)

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;

        var ofd = new OpenFileDialog
        {
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Todos os arquivos (*.*)|*.*",
            Title = "Importar Imagem para o Editor"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                using var bmp = new Bitmap(ofd.FileName);
                LoadBitmap(bmp, ofd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao abrir a imagem: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            BtnSaveAs_Click(sender, e);
        }
        else
        {
            SaveToFile(_currentFilePath);
        }
    }

    private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBitmap == null) return;

        var sfd = new SaveFileDialog
        {
            Filter = "Imagem PNG (*.png)|*.png|Imagem JPEG (*.jpg)|*.jpg",
            FileName = string.IsNullOrEmpty(_currentFilePath) ? ExportService.GenerateFileName("ShotMind_{yyyy-MM-dd_HH-mm-ss}", ImageFormatType.Png) : Path.GetFileName(_currentFilePath),
            Title = "Salvar Imagem Como..."
        };

        if (sfd.ShowDialog() == true)
        {
            SaveToFile(sfd.FileName);
        }
    }

    private void SaveToFile(string path)
    {
        if (_currentBitmap == null) return;

        try
        {
            using var finalBmp = ExportService.RenderAnnotatedBitmap(_currentBitmap, _annotations);
            var fmt = path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormatType.Jpg : ImageFormatType.Png;

            ExportService.SaveImageToFile(finalBmp, path, fmt);
            _currentFilePath = path;
            _hasUnsavedChanges = false;
            StatusInfo.Text = $"Salvo: {Path.GetFileName(path)}";

            MessageBox.Show("Imagem salva com sucesso!", "CrankoShot", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_hasUnsavedChanges) return true;

        var result = MessageBox.Show(
            "Existem alterações não salvas nesta imagem. Deseja descartá-las?",
            "Alterações Não Salvas",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            e.Cancel = true;
        }
        else
        {
            _currentBitmap?.Dispose();
            _currentBitmap = null;
        }
    }

    #endregion

    #region Canvas Drawing

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentBitmap == null || e.LeftButton != MouseButtonState.Pressed) return;

        Point pos = e.GetPosition(EditorAnnotationCanvas);

        if (_currentTool == ToolType.Text)
        {
            ShowTextInput(pos);
            return;
        }

        if (_currentTool == ToolType.StepMarker)
        {
            SaveUndoState();
            _hasUnsavedChanges = true;
            _annotations.Add(new AnnotationItem
            {
                Type = ToolType.StepMarker,
                StartPoint = pos,
                EndPoint = pos,
                Color = _currentColor,
                MarkerStyle = _markerStyle,
                MarkerNumber = _nextStepMarkerIndex,
                MarkerText = AnnotationItem.FormatMarkerText(_markerStyle, _nextStepMarkerIndex),
                MarkerTextColor = Colors.White,
                MarkerSize = _markerSize,
                MarkerHasFill = true,
                MarkerHasBorder = true
            });
            _nextStepMarkerIndex++;
            RedrawAllAnnotations();
            return;
        }

        _isDrawing = true;
        _startPoint = pos;
        SaveUndoState();
        _hasUnsavedChanges = true;

        double thickness = _currentTool switch
        {
            ToolType.Pen => _penThickness,
            ToolType.Highlighter => _highlighterThickness,
            ToolType.Line => _lineThickness,
            ToolType.Arrow => _arrowThickness,
            ToolType.Rectangle => _rectThickness,
            ToolType.Ellipse => _ellipseThickness,
            _ => 3.0
        };

        bool hasFill = false;
        Color fillColor = Colors.Transparent;
        if (_currentTool == ToolType.Rectangle)
        {
            if (_rectFillMode == "Translucent")
            {
                hasFill = true;
                fillColor = Color.FromArgb(75, _currentColor.R, _currentColor.G, _currentColor.B);
            }
            else if (_rectFillMode == "Solid")
            {
                hasFill = true;
                fillColor = _currentColor;
            }
        }
        else if (_currentTool == ToolType.Ellipse)
        {
            if (_ellipseFillMode == "Translucent")
            {
                hasFill = true;
                fillColor = Color.FromArgb(75, _currentColor.R, _currentColor.G, _currentColor.B);
            }
            else if (_ellipseFillMode == "Solid")
            {
                hasFill = true;
                fillColor = _currentColor;
            }
        }

        _currentAnnotation = new AnnotationItem
        {
            Type = _currentTool,
            StartPoint = pos,
            EndPoint = pos,
            Color = _currentColor,
            StrokeThickness = thickness,
            IsPencil = _currentTool == ToolType.Pen && _penIsPencil,
            HighlighterOpacity = _highlighterOpacity,
            HasFill = hasFill,
            FillColor = fillColor,
            ObfuscationStyle = _blurStyle,
            ObfuscationIntensity = _blurIntensity,
            Points = new List<Point> { pos }
        };

        EditorAnnotationCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        EditorAnnotationCanvas.Cursor = _currentTool == ToolType.Text ? Cursors.IBeam : Cursors.Cross;

        if (!_isDrawing || _currentAnnotation == null) return;

        Point pos = e.GetPosition(EditorAnnotationCanvas);
        _currentAnnotation.EndPoint = pos;

        if (_currentTool == ToolType.Pen || _currentTool == ToolType.Highlighter)
        {
            _currentAnnotation.Points.Add(pos);
        }

        RenderTempPreview(_currentAnnotation);
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        EditorAnnotationCanvas.ReleaseMouseCapture();

        if (!_isDrawing || _currentAnnotation == null) return;

        _isDrawing = false;
        _annotations.Add(_currentAnnotation);
        _currentAnnotation = null;

        if (_tempPreviewShape != null)
        {
            EditorAnnotationCanvas.Children.Remove(_tempPreviewShape);
            _tempPreviewShape = null;
        }

        RedrawAllAnnotations();
    }

    private void RenderTempPreview(AnnotationItem item)
    {
        if (_tempPreviewShape != null)
        {
            EditorAnnotationCanvas.Children.Remove(_tempPreviewShape);
            _tempPreviewShape = null;
        }

        var shape = CreateShapeElement(item);
        if (shape != null)
        {
            _tempPreviewShape = shape;
            EditorAnnotationCanvas.Children.Add(_tempPreviewShape);
        }
    }

    private void RedrawAllAnnotations()
    {
        EditorAnnotationCanvas.Children.Clear();
        foreach (var ann in _annotations)
        {
            var shape = CreateShapeElement(ann);
            if (shape != null)
            {
                EditorAnnotationCanvas.Children.Add(shape);
            }
        }
    }

    private FrameworkElement? CreateShapeElement(AnnotationItem item)
    {
        var brush = new SolidColorBrush(item.Color);

        switch (item.Type)
        {
            case ToolType.Line:
                return new System.Windows.Shapes.Line
                {
                    X1 = item.StartPoint.X,
                    Y1 = item.StartPoint.Y,
                    X2 = item.EndPoint.X,
                    Y2 = item.EndPoint.Y,
                    Stroke = brush,
                    StrokeThickness = item.StrokeThickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

            case ToolType.Arrow:
                return CreateArrowGeometry(item, brush);

            case ToolType.Rectangle:
                double rx = Math.Min(item.StartPoint.X, item.EndPoint.X);
                double ry = Math.Min(item.StartPoint.Y, item.EndPoint.Y);
                double rw = Math.Abs(item.StartPoint.X - item.EndPoint.X);
                double rh = Math.Abs(item.StartPoint.Y - item.EndPoint.Y);
                Brush rectFill = Brushes.Transparent;
                if (item.HasFill)
                {
                    rectFill = item.FillColor.A > 0
                        ? new SolidColorBrush(item.FillColor)
                        : new SolidColorBrush(Color.FromArgb(75, item.Color.R, item.Color.G, item.Color.B));
                }
                var r = new System.Windows.Shapes.Rectangle
                {
                    Stroke = brush,
                    StrokeThickness = item.StrokeThickness,
                    Fill = rectFill,
                    Width = rw,
                    Height = rh
                };
                Canvas.SetLeft(r, rx);
                Canvas.SetTop(r, ry);
                return r;

            case ToolType.Ellipse:
                double ex = Math.Min(item.StartPoint.X, item.EndPoint.X);
                double ey = Math.Min(item.StartPoint.Y, item.EndPoint.Y);
                double ew = Math.Abs(item.StartPoint.X - item.EndPoint.X);
                double eh = Math.Abs(item.StartPoint.Y - item.EndPoint.Y);
                Brush ellipseFill = Brushes.Transparent;
                if (item.HasFill)
                {
                    ellipseFill = item.FillColor.A > 0
                        ? new SolidColorBrush(item.FillColor)
                        : new SolidColorBrush(Color.FromArgb(75, item.Color.R, item.Color.G, item.Color.B));
                }
                var el = new System.Windows.Shapes.Ellipse
                {
                    Stroke = brush,
                    StrokeThickness = item.StrokeThickness,
                    Fill = ellipseFill,
                    Width = ew,
                    Height = eh
                };
                Canvas.SetLeft(el, ex);
                Canvas.SetTop(el, ey);
                return el;

            case ToolType.Pen:
                if (item.Points.Count < 2) return null;
                var penBrush = item.IsPencil
                    ? new SolidColorBrush(Color.FromArgb(170, item.Color.R, item.Color.G, item.Color.B))
                    : brush;
                var poly = new Polyline
                {
                    Stroke = penBrush,
                    StrokeThickness = item.StrokeThickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                foreach (var p in item.Points) poly.Points.Add(p);
                return poly;

            case ToolType.Highlighter:
                if (item.Points.Count < 2) return null;
                byte alpha = item.HighlighterOpacity > 0 ? item.HighlighterOpacity : (byte)90;
                double hlThickness = item.StrokeThickness > 0 ? item.StrokeThickness : 20;
                var hl = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(alpha, item.Color.R, item.Color.G, item.Color.B)),
                    StrokeThickness = hlThickness,
                    StrokeLineJoin = PenLineJoin.Bevel,
                    StrokeStartLineCap = PenLineCap.Square,
                    StrokeEndLineCap = PenLineCap.Square
                };
                foreach (var p in item.Points) hl.Points.Add(p);
                return hl;

            case ToolType.Text:
                var tb = new TextBlock
                {
                    Text = item.Text,
                    Foreground = brush,
                    FontSize = item.FontSize > 0 ? item.FontSize : 18,
                    FontWeight = FontWeights.Bold
                };
                Canvas.SetLeft(tb, item.StartPoint.X);
                Canvas.SetTop(tb, item.StartPoint.Y);
                return tb;

            case ToolType.Blur:
                double bx = Math.Min(item.StartPoint.X, item.EndPoint.X);
                double by = Math.Min(item.StartPoint.Y, item.EndPoint.Y);
                double bw = Math.Abs(item.StartPoint.X - item.EndPoint.X);
                double bh = Math.Abs(item.StartPoint.Y - item.EndPoint.Y);

                if (bw > 2 && bh > 2 && _currentBitmap != null)
                {
                    var cropRect = new System.Drawing.Rectangle((int)bx, (int)by, (int)bw, (int)bh);
                    int intensity = item.ObfuscationIntensity > 0 ? item.ObfuscationIntensity : _blurIntensity;
                    var bs = ExportService.CreateObfuscatedRegionBitmap(_currentBitmap, cropRect, item.ObfuscationStyle, intensity);
                    if (bs != null)
                    {
                        var img = new System.Windows.Controls.Image
                        {
                            Source = bs,
                            Width = bw,
                            Height = bh,
                            Stretch = Stretch.Fill
                        };
                        Canvas.SetLeft(img, bx);
                        Canvas.SetTop(img, by);
                        return img;
                    }
                }

                var blurRect = new System.Windows.Shapes.Rectangle
                {
                    Fill = new SolidColorBrush(Color.FromArgb(160, 40, 40, 45)),
                    Stroke = new SolidColorBrush(Color.FromArgb(180, 139, 92, 246)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    Width = bw,
                    Height = bh
                };
                Canvas.SetLeft(blurRect, bx);
                Canvas.SetTop(blurRect, by);
                return blurRect;

            case ToolType.StepMarker:
                {
                    double size = item.MarkerSize > 0 ? item.MarkerSize : 26.0;
                    double half = size / 2.0;

                    var grid = new Grid
                    {
                        Width = size,
                        Height = size
                    };

                    var ellipse = new System.Windows.Shapes.Ellipse
                    {
                        Width = size,
                        Height = size,
                        Fill = item.MarkerHasFill ? brush : Brushes.Transparent,
                        Stroke = item.MarkerHasBorder ? Brushes.White : Brushes.Transparent,
                        StrokeThickness = item.MarkerHasBorder ? 1.5 : 0
                    };
                    grid.Children.Add(ellipse);

                    if (item.MarkerStyle == StepMarkerStyle.Dots)
                    {
                        var dot = new System.Windows.Shapes.Ellipse
                        {
                            Width = Math.Max(4, size * 0.35),
                            Height = Math.Max(4, size * 0.35),
                            Fill = Brushes.White,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = System.Windows.VerticalAlignment.Center
                        };
                        grid.Children.Add(dot);
                    }
                    else if (!string.IsNullOrEmpty(item.MarkerText))
                    {
                        var txtBrush = new SolidColorBrush(item.MarkerTextColor.A > 0 ? item.MarkerTextColor : Colors.White);
                        double fontSize = Math.Max(9.0, size * 0.46);
                        var textBlock = new TextBlock
                        {
                            Text = item.MarkerText,
                            Foreground = txtBrush,
                            FontSize = fontSize,
                            FontWeight = FontWeights.Bold,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = System.Windows.VerticalAlignment.Center,
                            TextAlignment = TextAlignment.Center
                        };
                        grid.Children.Add(textBlock);
                    }

                    Canvas.SetLeft(grid, item.StartPoint.X - half);
                    Canvas.SetTop(grid, item.StartPoint.Y - half);
                    return grid;
                }
        }

        return null;
    }

    private System.Windows.Shapes.Path CreateArrowGeometry(AnnotationItem item, SolidColorBrush brush)
    {
        Point p1 = item.StartPoint;
        Point p2 = item.EndPoint;

        double theta = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
        double arrowHeadLength = Math.Max(12, item.StrokeThickness * 3.5);
        double arrowHeadAngle = Math.PI / 6;

        Point pArrow1 = new Point(p2.X - arrowHeadLength * Math.Cos(theta - arrowHeadAngle), p2.Y - arrowHeadLength * Math.Sin(theta - arrowHeadAngle));
        Point pArrow2 = new Point(p2.X - arrowHeadLength * Math.Cos(theta + arrowHeadAngle), p2.Y - arrowHeadLength * Math.Sin(theta + arrowHeadAngle));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(p1, false, false);
            ctx.LineTo(p2, true, false);
            ctx.BeginFigure(pArrow1, true, true);
            ctx.LineTo(p2, true, false);
            ctx.LineTo(pArrow2, true, false);
        }

        return new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stroke = brush,
            Fill = brush,
            StrokeThickness = item.StrokeThickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
    }

    #endregion

    #region Text Tool

    private void ShowTextInput(Point clickPos)
    {
        EditorInlineText.Text = string.Empty;
        EditorInlineText.FontSize = _textFontSize;
        EditorInlineText.Foreground = new SolidColorBrush(_currentColor);
        Canvas.SetLeft(EditorInlineText, clickPos.X);
        Canvas.SetTop(EditorInlineText, clickPos.Y);
        EditorInlineText.Visibility = Visibility.Visible;
        EditorInlineText.Focus();
    }

    private void EditorInlineText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitInlineText();
        else if (e.Key == Key.Escape) EditorInlineText.Visibility = Visibility.Collapsed;
    }

    private void EditorInlineText_LostFocus(object sender, RoutedEventArgs e) => CommitInlineText();

    private void CommitInlineText()
    {
        if (EditorInlineText.Visibility != Visibility.Visible) return;

        string text = EditorInlineText.Text.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            SaveUndoState();
            _hasUnsavedChanges = true;
            double x = Canvas.GetLeft(EditorInlineText);
            double y = Canvas.GetTop(EditorInlineText);

            _annotations.Add(new AnnotationItem
            {
                Type = ToolType.Text,
                StartPoint = new Point(x, y),
                Text = text,
                Color = _currentColor,
                FontSize = _textFontSize
            });

            RedrawAllAnnotations();
        }

        EditorInlineText.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Undo / Redo & Tools

    private void SaveUndoState()
    {
        _undoStack.Push(_annotations.Select(a => a.Clone()).ToList());
        _redoStack.Clear();
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count > 0)
        {
            _redoStack.Push(_annotations.Select(a => a.Clone()).ToList());
            var prev = _undoStack.Pop();
            _annotations.Clear();
            _annotations.AddRange(prev);
            ResyncNextStepMarkerIndex();
            RedrawAllAnnotations();
        }
    }

    private void BtnRedo_Click(object sender, RoutedEventArgs e)
    {
        if (_redoStack.Count > 0)
        {
            _undoStack.Push(_annotations.Select(a => a.Clone()).ToList());
            var next = _redoStack.Pop();
            _annotations.Clear();
            _annotations.AddRange(next);
            ResyncNextStepMarkerIndex();
            RedrawAllAnnotations();
        }
    }

    private void ResyncNextStepMarkerIndex()
    {
        int maxUsed = _annotations
            .Where(a => a.Type == ToolType.StepMarker)
            .Select(a => a.MarkerNumber)
            .DefaultIfEmpty(0)
            .Max();
        _nextStepMarkerIndex = maxUsed + 1;
    }

    private void BtnTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && Enum.TryParse<ToolType>(tag, out var tool))
        {
            _currentTool = tool;
            UpdateToolSelection();
        }
    }

    private void UpdateToolSelection()
    {
        var buttons = new[] { BtnToolPen, BtnToolHighlighter, BtnToolLine, BtnToolArrow, BtnToolRect, BtnToolEllipse, BtnToolText, BtnToolBlur, BtnToolMarker };
        var activeBg = (Brush)FindResource("AccentPrimaryBrush");
        var activeFg = (Brush)FindResource("AccentForegroundBrush");
        var inactiveFg = (Brush)FindResource("TextPrimaryBrush");
        var activeBorder = (Brush)FindResource("AccentBorderBrush") ?? new SolidColorBrush(Color.FromRgb(216, 180, 254));

        foreach (var b in buttons)
        {
            if (b != null)
            {
                bool active = b.Tag?.ToString() == _currentTool.ToString();
                b.Background = active ? activeBg : Brushes.Transparent;
                b.BorderBrush = active ? activeBorder : Brushes.Transparent;
                b.BorderThickness = active ? new Thickness(1) : new Thickness(0);
                b.Foreground = active ? activeFg : inactiveFg;

                if (b.Content is TextBlock tb)
                {
                    tb.Foreground = active ? activeFg : inactiveFg;
                }
            }
        }
    }

    #region Flyout / Tool Options Handlers

    private void ToolButton_MouseEnter(object sender, MouseEventArgs e)
    {
        _flyoutCloseTimer.Stop();
        if (sender is Button btn)
        {
            var popup = FindAssociatedPopup(btn);
            if (popup != null)
            {
                if (_activeFlyout != null && _activeFlyout != popup)
                {
                    _activeFlyout.IsOpen = false;
                }
                _activeFlyout = popup;
                _activeFlyout.IsOpen = true;
            }
        }
    }

    private void ToolButton_MouseLeave(object sender, MouseEventArgs e)
    {
        _flyoutCloseTimer.Start();
    }

    private void Popup_MouseEnter(object sender, MouseEventArgs e)
    {
        _flyoutCloseTimer.Stop();
    }

    private void Popup_MouseLeave(object sender, MouseEventArgs e)
    {
        _flyoutCloseTimer.Start();
    }

    private void CloseAllFlyouts()
    {
        if (_activeFlyout != null)
        {
            _activeFlyout.IsOpen = false;
            _activeFlyout = null;
        }
        if (PopupEditorPenMenu != null) PopupEditorPenMenu.IsOpen = false;
        if (PopupEditorHighlighterMenu != null) PopupEditorHighlighterMenu.IsOpen = false;
        if (PopupEditorLineMenu != null) PopupEditorLineMenu.IsOpen = false;
        if (PopupEditorArrowMenu != null) PopupEditorArrowMenu.IsOpen = false;
        if (PopupEditorRectMenu != null) PopupEditorRectMenu.IsOpen = false;
        if (PopupEditorEllipseMenu != null) PopupEditorEllipseMenu.IsOpen = false;
        if (PopupEditorTextMenu != null) PopupEditorTextMenu.IsOpen = false;
        if (PopupEditorBlurMenu != null) PopupEditorBlurMenu.IsOpen = false;
        if (PopupEditorMarkerMenu != null) PopupEditorMarkerMenu.IsOpen = false;
        if (PopupEditorColorMenu != null) PopupEditorColorMenu.IsOpen = false;
    }

    private System.Windows.Controls.Primitives.Popup? FindAssociatedPopup(Button btn)
    {
        if (btn == BtnToolPen) return PopupEditorPenMenu;
        if (btn == BtnToolHighlighter) return PopupEditorHighlighterMenu;
        if (btn == BtnToolLine) return PopupEditorLineMenu;
        if (btn == BtnToolArrow) return PopupEditorArrowMenu;
        if (btn == BtnToolRect) return PopupEditorRectMenu;
        if (btn == BtnToolEllipse) return PopupEditorEllipseMenu;
        if (btn == BtnToolText) return PopupEditorTextMenu;
        if (btn == BtnToolBlur) return PopupEditorBlurMenu;
        if (btn == BtnToolMarker) return PopupEditorMarkerMenu;
        if (btn == BtnColorPalette) return PopupEditorColorMenu;
        return null;
    }

    // Pen Options
    private void BtnPenMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag)
        {
            _penIsPencil = tag == "Pencil";
            BtnEditorPenNormal.Tag = _penIsPencil ? "" : "Active";
            BtnEditorPenPencil.Tag = _penIsPencil ? "Active" : "";
            _currentTool = ToolType.Pen;
            UpdateToolSelection();
        }
    }

    private void BtnPenThicknessPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && double.TryParse(b.Tag?.ToString(), out double th))
        {
            _penThickness = th;
            SliderEditorPenThickness.Value = th;
            TxtEditorPenThicknessValue.Text = $"{th:0} px";
            _currentTool = ToolType.Pen;
            UpdateToolSelection();
        }
    }

    private void SliderEditorPenThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtEditorPenThicknessValue != null)
        {
            _penThickness = Math.Round(e.NewValue);
            TxtEditorPenThicknessValue.Text = $"{_penThickness:0} px";
        }
    }

    // Highlighter Options
    private void BtnHlThicknessPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && double.TryParse(b.Tag?.ToString(), out double th))
        {
            _highlighterThickness = th;
            SliderEditorHlThickness.Value = th;
            TxtEditorHlThicknessValue.Text = $"{th:0} px";
            _currentTool = ToolType.Highlighter;
            UpdateToolSelection();
        }
    }

    private void SliderEditorHlThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtEditorHlThicknessValue != null)
        {
            _highlighterThickness = Math.Round(e.NewValue);
            TxtEditorHlThicknessValue.Text = $"{_highlighterThickness:0} px";
        }
    }

    private void BtnHlOpacityPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && byte.TryParse(b.Tag?.ToString(), out byte op))
        {
            _highlighterOpacity = op;
            SliderEditorHlOpacity.Value = op;
            TxtEditorHlOpacityValue.Text = $"{(int)(op / 255.0 * 100)}%";
            _currentTool = ToolType.Highlighter;
            UpdateToolSelection();
        }
    }

    private void SliderEditorHlOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtEditorHlOpacityValue != null)
        {
            _highlighterOpacity = (byte)Math.Clamp(Math.Round(e.NewValue), 10, 255);
            TxtEditorHlOpacityValue.Text = $"{(int)(_highlighterOpacity / 255.0 * 100)}%";
        }
    }

    // Line Options
    private void BtnLineThicknessPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && double.TryParse(b.Tag?.ToString(), out double th))
        {
            _lineThickness = th;
            SliderEditorLineThickness.Value = th;
            TxtEditorLineThicknessValue.Text = $"{th:0} px";
            _currentTool = ToolType.Line;
            UpdateToolSelection();
        }
    }

    private void SliderEditorLineThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtEditorLineThicknessValue != null)
        {
            _lineThickness = Math.Round(e.NewValue);
            TxtEditorLineThicknessValue.Text = $"{_lineThickness:0} px";
        }
    }

    // Arrow Options
    private void BtnArrowThicknessPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && double.TryParse(b.Tag?.ToString(), out double th))
        {
            _arrowThickness = th;
            SliderEditorArrowThickness.Value = th;
            TxtEditorArrowThicknessValue.Text = $"{th:0} px";
            _currentTool = ToolType.Arrow;
            UpdateToolSelection();
        }
    }

    private void SliderEditorArrowThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtEditorArrowThicknessValue != null)
        {
            _arrowThickness = Math.Round(e.NewValue);
            TxtEditorArrowThicknessValue.Text = $"{_arrowThickness:0} px";
        }
    }

    // Rectangle Options
    private void BtnRectFill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag)
        {
            _rectFillMode = tag;
            BtnRectFillNone.Tag = tag == "None" ? "Active" : "";
            BtnRectFillTranslucent.Tag = tag == "Translucent" ? "Active" : "";
            BtnRectFillSolid.Tag = tag == "Solid" ? "Active" : "";
            _currentTool = ToolType.Rectangle;
            UpdateToolSelection();
        }
    }

    private void BtnRectThicknessPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && double.TryParse(b.Tag?.ToString(), out double th))
        {
            _rectThickness = th;
            SliderEditorRectThickness.Value = th;
            TxtEditorRectThicknessValue.Text = $"{th:0} px";
            _currentTool = ToolType.Rectangle;
            UpdateToolSelection();
        }
    }

    private void SliderEditorRectThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtEditorRectThicknessValue != null)
        {
            _rectThickness = Math.Round(e.NewValue);
            TxtEditorRectThicknessValue.Text = $"{_rectThickness:0} px";
        }
    }

    // Ellipse Options
    private void BtnEllipseFill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag)
        {
            _ellipseFillMode = tag;
            BtnEllipseFillNone.Tag = tag == "None" ? "Active" : "";
            BtnEllipseFillTranslucent.Tag = tag == "Translucent" ? "Active" : "";
            BtnEllipseFillSolid.Tag = tag == "Solid" ? "Active" : "";
            _currentTool = ToolType.Ellipse;
            UpdateToolSelection();
        }
    }

    private void BtnEllipseThicknessPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && double.TryParse(b.Tag?.ToString(), out double th))
        {
            _ellipseThickness = th;
            SliderEditorEllipseThickness.Value = th;
            TxtEditorEllipseThicknessValue.Text = $"{th:0} px";
            _currentTool = ToolType.Ellipse;
            UpdateToolSelection();
        }
    }

    private void SliderEditorEllipseThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtEditorEllipseThicknessValue != null)
        {
            _ellipseThickness = Math.Round(e.NewValue);
            TxtEditorEllipseThicknessValue.Text = $"{_ellipseThickness:0} px";
        }
    }

    // Text Options
    private void BtnTextSizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && double.TryParse(b.Tag?.ToString(), out double sz))
        {
            _textFontSize = sz;
            SliderEditorFontSize.Value = sz;
            TxtEditorFontSizeValue.Text = $"{sz:0} px";
            _currentTool = ToolType.Text;
            UpdateToolSelection();
        }
    }

    private void SliderEditorFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtEditorFontSizeValue != null)
        {
            _textFontSize = Math.Round(e.NewValue);
            TxtEditorFontSizeValue.Text = $"{_textFontSize:0} px";
        }
    }

    // Blur / Obfuscation Options
    private void BtnEditorBlurMode_Click(object sender, RoutedEventArgs e)
    {
        _blurStyle = ObfuscationStyle.Blur;
        BtnEditorBlurMode.Tag = "Active";
        BtnEditorPixelateMode.Tag = "";
        _currentTool = ToolType.Blur;
        UpdateToolSelection();
    }

    private void BtnEditorPixelateMode_Click(object sender, RoutedEventArgs e)
    {
        _blurStyle = ObfuscationStyle.Pixelate;
        BtnEditorBlurMode.Tag = "";
        BtnEditorPixelateMode.Tag = "Active";
        _currentTool = ToolType.Blur;
        UpdateToolSelection();
    }

    private void BtnEditorBlurPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && int.TryParse(b.Tag?.ToString(), out int intensity))
        {
            _blurIntensity = intensity;
            SliderEditorBlurIntensity.Value = intensity;
            TxtEditorBlurIntensityValue.Text = $"{intensity} px";
            _currentTool = ToolType.Blur;
            UpdateToolSelection();
        }
    }

    private void SliderEditorBlurIntensity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtEditorBlurIntensityValue != null)
        {
            _blurIntensity = (int)Math.Round(e.NewValue);
            TxtEditorBlurIntensityValue.Text = $"{_blurIntensity} px";
        }
    }

    // Marker Options
    private void BtnEditorMarkerStyle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && Enum.TryParse<StepMarkerStyle>(b.Tag?.ToString(), out var style))
        {
            _markerStyle = style;
            BtnEditorMarkerNumbers.Tag = style == StepMarkerStyle.Numbers ? "Active" : "";
            BtnEditorMarkerUpper.Tag = style == StepMarkerStyle.Uppercase ? "Active" : "";
            BtnEditorMarkerLower.Tag = style == StepMarkerStyle.Lowercase ? "Active" : "";
            BtnEditorMarkerDots.Tag = style == StepMarkerStyle.Dots ? "Active" : "";
            _currentTool = ToolType.StepMarker;
            UpdateToolSelection();
        }
    }

    private void BtnEditorMarkerSizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && double.TryParse(b.Tag?.ToString(), out double sz))
        {
            _markerSize = sz;
            TxtEditorMarkerSizeValue.Text = $"{sz:0} px";
            _currentTool = ToolType.StepMarker;
            UpdateToolSelection();
        }
    }

    private void BtnEditorRestartMarkerSequence_Click(object sender, RoutedEventArgs e)
    {
        _nextStepMarkerIndex = 1;
        MessageBox.Show("A contagem dos marcadores foi reiniciada para o número 1 ou letra A.", "CrankoShot", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Color Palette & Hex Input
    private void BtnColorPalette_Click(object sender, RoutedEventArgs e)
    {
        _flyoutCloseTimer.Stop();
        PopupEditorColorMenu.IsOpen = !PopupEditorColorMenu.IsOpen;
    }

    private void BtnApplyEditorColorHex_Click(object sender, RoutedEventArgs e)
    {
        ApplyHexColor(TxtEditorColorHex.Text);
    }

    private void TxtEditorColorHex_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyHexColor(TxtEditorColorHex.Text);
            e.Handled = true;
        }
    }

    private void ApplyHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        hex = hex.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            _currentColor = c;
            UpdateColorSelection();
            PopupEditorColorMenu.IsOpen = false;
        }
        catch
        {
            MessageBox.Show("Código hexadecimal inválido. Utilize o formato #RRGGBB.", "Cor Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    #endregion

    private void BtnColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            _currentColor = (Color)ColorConverter.ConvertFromString(hex);
            UpdateColorSelection();
        }
    }

    private void UpdateColorSelection()
    {
        var colorButtons = new (Button, string)[]
        {
            (BtnColorPurple, "#8B5CF6"),
            (BtnColorRed, "#EF4444"),
            (BtnColorGreen, "#10B981"),
            (BtnColorBlue, "#3B82F6"),
            (BtnColorYellow, "#F59E0B"),
            (BtnColorWhite, "#FFFFFF")
        };

        string currentHex = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";

        foreach (var (b, hex) in colorButtons)
        {
            if (b == null) continue;
            bool isMatch = string.Equals(hex, currentHex, StringComparison.OrdinalIgnoreCase);
            b.BorderBrush = isMatch ? (Brush)FindResource("AccentBorderBrush") ?? Brushes.White : Brushes.Transparent;
            b.BorderThickness = isMatch ? new Thickness(2) : new Thickness(0);
        }

        if (EllipseEditorColorPreview != null)
        {
            EllipseEditorColorPreview.Fill = new SolidColorBrush(_currentColor);
        }
        if (TxtEditorColorHex != null)
        {
            TxtEditorColorHex.Text = currentHex;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var sm = ShortcutManager.Instance;
        var settings = SettingsService.Instance.Current;

        if (sm.IsActionTriggered(ShortcutManager.ActionCancel, e, settings) ||
            sm.IsActionTriggered(ShortcutManager.ActionCloseEditor, e, settings))
        {
            Close();
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionCopy, e, settings))
        {
            BtnCopy_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionSave, e, settings))
        {
            BtnSave_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionPin, e, settings))
        {
            BtnPin_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionUndo, e, settings))
        {
            BtnUndo_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionRedo, e, settings))
        {
            BtnRedo_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionOcr, e, settings))
        {
            BtnOcr_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionAi, e, settings))
        {
            BtnAskAi_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    #endregion

    #region Actions: Copy, OCR, AI, Pin

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBitmap == null) return;
        var finalBmp = ExportService.RenderAnnotatedBitmap(_currentBitmap, _annotations);
        var pinnedWin = new PinnedCaptureWindow(finalBmp);
        pinnedWin.Show();
        SoundService.PlayCaptureSound();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBitmap == null) return;
        using var finalBmp = ExportService.RenderAnnotatedBitmap(_currentBitmap, _annotations);
        ExportService.CopyToClipboard(finalBmp);
        SoundService.PlayCaptureSound();
        MessageBox.Show("Imagem copiada para a área de transferência com sucesso!", "CrankoShot", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnOcr_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_currentBitmap == null) return;
            using var finalBmp = ExportService.RenderAnnotatedBitmap(_currentBitmap, _annotations);

            var aiWindow = new AiResultWindow();
            aiWindow.Show();
            aiWindow.Activate();

            _ = aiWindow.ExecuteOcrAsync(finalBmp);
        }
        catch (Exception ex)
        {
            Logger.LogException("FullEditor", "BtnOcr_Click", ex);
        }
    }

    private void BtnAskAi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_currentBitmap == null) return;
            using var finalBmp = ExportService.RenderAnnotatedBitmap(_currentBitmap, _annotations);

            var aiWindow = new AiResultWindow();
            aiWindow.Show();
            aiWindow.Activate();
            aiWindow.StartChatMode(finalBmp);
        }
        catch (Exception ex)
        {
            Logger.LogException("FullEditor", "BtnAskAi_Click", ex);
        }
    }

    private void BtnAiMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBitmap == null) return;

        var contextMenu = new ContextMenu();
        AddAiItem(contextMenu, "✦ Analisar Imagem Completa", AiActionType.Analyze);
        AddAiItem(contextMenu, "✦ Extrair Texto Estruturado", AiActionType.ExtractText);
        AddAiItem(contextMenu, "✦ Traduzir para Português", AiActionType.Translate);
        AddAiItem(contextMenu, "✦ Resumir Conteúdo", AiActionType.Summarize);
        AddAiItem(contextMenu, "✦ Diagnosticar Erro", AiActionType.ExplainError);
        AddAiItem(contextMenu, "✦ Perguntar à IA...", AiActionType.AskQuestion);
        AddAiItem(contextMenu, "✦ Identificar Elementos", AiActionType.IdentifyElements);
        AddAiItem(contextMenu, "✦ Pesquisar na Web com Gemini", AiActionType.SearchWeb);

        contextMenu.PlacementTarget = BtnAiMenu;
        contextMenu.IsOpen = true;
    }

    private void AddAiItem(ContextMenu menu, string header, AiActionType action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (s, e) =>
        {
            try
            {
                if (_currentBitmap == null) return;
                using var finalBmp = ExportService.RenderAnnotatedBitmap(_currentBitmap, _annotations);

                var aiWindow = new AiResultWindow();
                aiWindow.Show();
                aiWindow.Activate();

                if (action == AiActionType.AskQuestion)
                {
                    aiWindow.StartChatMode(finalBmp);
                }
                else
                {
                    _ = aiWindow.ExecuteGeminiActionAsync(action, finalBmp);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("FullEditor", "AddAiItem_Click", ex);
            }
        };
        menu.Items.Add(item);
    }

    #endregion
}
