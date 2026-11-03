using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using ShotMind.Core;
using ShotMind.Core.Models;
using ShotMind.Core.Services;
using Rectangle = System.Drawing.Rectangle;

namespace ShotMind.UI.Views;

public partial class CaptureOverlayWindow : Window
{
    private enum HitZone
    {
        None,
        Inside,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left
    }

    private enum InteractionMode
    {
        None,
        CreatingSelection,
        MovingSelection,
        ResizingSelection,
        DrawingAnnotation
    }

    private Bitmap? _fullBitmap;
    private System.Drawing.Rectangle _virtualBounds;
    private Rect _selectionRect = Rect.Empty;
    private Rect _initialSelectionRect = Rect.Empty;

    private InteractionMode _mode = InteractionMode.None;
    private HitZone _currentResizeZone = HitZone.None;
    private Point _startPoint;

    private ToolType _currentTool = ToolType.Select;
    private Color _currentColor = (Color)ColorConverter.ConvertFromString("#8B5CF6");
    private double _currentStrokeThickness = 3.0;

    // Step Marker State
    private StepMarkerStyle _stepMarkerStyle = StepMarkerStyle.Numbers;
    private int _nextStepMarkerIndex = 1;
    private double _stepMarkerSize = 26.0;

    // Per-Tool Color & Thickness Memory
    private readonly Dictionary<ToolType, Color> _toolColors = new();
    private readonly Dictionary<ToolType, double> _toolThicknesses = new();

    private readonly List<AnnotationItem> _annotations = new();
    private readonly Stack<List<AnnotationItem>> _undoStack = new();
    private readonly Stack<List<AnnotationItem>> _redoStack = new();

    private FrameworkElement? _tempPreviewShape;
    private AnnotationItem? _currentAnnotation;

    // Flyout timer fields
    private System.Windows.Threading.DispatcherTimer? _flyoutTimer;
    private System.Windows.Controls.Primitives.Popup? _activeFlyout;

    // Visual Cursor state (80x80 1:1 pixel accuracy via CrosshairService)
    private WriteableBitmap? _cursorBitmap;
    private readonly int[] _cursorPixels = new int[CrosshairService.GridSize * CrosshairService.GridSize];
    private readonly Int32Rect _cursorDirtyRect = new(0, 0, CrosshairService.GridSize, CrosshairService.GridSize);
    private byte[]? _cachedCrosshairMask;
    private int[]? _cachedScreenPixels;
    private int _cachedBitmapWidth;
    private int _cachedBitmapHeight;

    // Obfuscation state
    private ObfuscationStyle _currentObfuscationStyle = ObfuscationStyle.Pixelate;
    private int _currentObfuscationIntensity = 12;

    // Color picker state
    private bool _isUpdatingColorPickerText = false;

    public CaptureOverlayWindow()
    {
        InitializeComponent();
        InitializeFlyoutTimers();
        _cursorBitmap = new WriteableBitmap(CrosshairService.GridSize, CrosshairService.GridSize, 96, 96, PixelFormats.Bgra32, null);
        CursorInvertImage.Source = _cursorBitmap;
        _currentColor = ThemeService.Instance.CurrentTheme.Primary;
        InitToolDefaults();
        UpdateColorButtonStates();
        ThemeService.Instance.ThemeChanged += OnThemeChanged;
        OverlayCanvas.MouseLeave += (s, e) => { VisualCursorIndicator.Visibility = Visibility.Collapsed; };
        DrawingToolbar.SizeChanged += (s, e) => PositionToolbar();
        ActionsToolbar.SizeChanged += (s, e) => PositionToolbar();
    }

    private void OnThemeChanged(ThemeInfo theme)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnThemeChanged(theme));
            return;
        }

        DrawingToolbar.SetResourceReference(Border.BorderBrushProperty, "AccentPrimaryBrush");
        ActionsToolbar.SetResourceReference(Border.BorderBrushProperty, "AccentPrimaryBrush");
        UpdateToolButtonStates();
        UpdateColorButtonStates();
    }

    private void InitToolDefaults()
    {
        _toolColors[ToolType.Pen] = _currentColor;
        _toolColors[ToolType.Highlighter] = Color.FromRgb(245, 158, 11);
        _toolColors[ToolType.Arrow] = _currentColor;
        _toolColors[ToolType.Rectangle] = _currentColor;
        _toolColors[ToolType.Ellipse] = _currentColor;
        _toolColors[ToolType.Line] = _currentColor;
        _toolColors[ToolType.Text] = _currentColor;
        _toolColors[ToolType.StepMarker] = _currentColor;

        _toolThicknesses[ToolType.Pen] = 3.0;
        _toolThicknesses[ToolType.Highlighter] = 16.0;
        _toolThicknesses[ToolType.Arrow] = 3.0;
        _toolThicknesses[ToolType.Rectangle] = 3.0;
        _toolThicknesses[ToolType.Ellipse] = 3.0;
        _toolThicknesses[ToolType.Line] = 3.0;
        _toolThicknesses[ToolType.Text] = 16.0;
        _toolThicknesses[ToolType.StepMarker] = 26.0;
    }

    private void InitializeFlyoutTimers()
    {
        _flyoutTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _flyoutTimer.Tick += (s, e) =>
        {
            _flyoutTimer.Stop();
            if (_activeFlyout != null)
            {
                _activeFlyout.IsOpen = false;
                _activeFlyout = null;
            }
        };
    }

    public void StartCapture(Bitmap fullScreenBitmap, System.Drawing.Rectangle bounds)
    {
        _fullBitmap = fullScreenBitmap;
        _virtualBounds = bounds;

        _cachedBitmapWidth = fullScreenBitmap.Width;
        _cachedBitmapHeight = fullScreenBitmap.Height;
        _cachedScreenPixels = new int[_cachedBitmapWidth * _cachedBitmapHeight];
        var data = fullScreenBitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, _cachedBitmapWidth, _cachedBitmapHeight),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, _cachedScreenPixels, 0, _cachedScreenPixels.Length);
        }
        finally
        {
            fullScreenBitmap.UnlockBits(data);
        }

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        ScreenGeometry.Rect = new Rect(0, 0, bounds.Width, bounds.Height);
        SelectionGeometry.Rect = Rect.Empty;

        BackgroundImage.Source = ScreenCaptureService.ToBitmapSource(fullScreenBitmap);

        ResetState();
        Show();
        Activate();
        Focus();
    }

    private void ResetState()
    {
        _selectionRect = Rect.Empty;
        _initialSelectionRect = Rect.Empty;
        _mode = InteractionMode.None;
        _annotations.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        _nextStepMarkerIndex = 1;
        _currentTool = ToolType.Select;
        _currentObfuscationStyle = SettingsService.Instance.Current.DefaultObfuscationStyle;
        _currentObfuscationIntensity = Math.Clamp(SettingsService.Instance.Current.ObfuscationIntensity, 2, 40);
        _cachedCrosshairMask = CrosshairService.BuildMask(SettingsService.Instance.Current);

        CloseAllFlyouts();
        VisualCursorIndicator.Visibility = Visibility.Collapsed;

        SelectionGeometry.Rect = Rect.Empty;
        SelectionBorder.Visibility = Visibility.Collapsed;
        DimensionBadge.Visibility = Visibility.Collapsed;
        SetHandlesVisibility(Visibility.Collapsed);
        DrawingToolbar.Visibility = Visibility.Collapsed;
        ActionsToolbar.Visibility = Visibility.Collapsed;
        AnnotationCanvas.Visibility = Visibility.Collapsed;
        AnnotationCanvas.Children.Clear();
        InlineTextInput.Visibility = Visibility.Collapsed;

        DrawingToolbar.SetResourceReference(Border.BorderBrushProperty, "AccentPrimaryBrush");
        ActionsToolbar.SetResourceReference(Border.BorderBrushProperty, "AccentPrimaryBrush");
        UpdateToolButtonStates();
        UpdateColorButtonStates();
    }

    #region Hit Testing & Cursor Management

    private HitZone GetHitZone(Point p)
    {
        if (_selectionRect.IsEmpty) return HitZone.None;

        var r = _selectionRect;
        const double handleTol = 10.0;
        const double edgeTol = 6.0;

        // 1. Check 4 corners
        if (Math.Abs(p.X - r.Left) <= handleTol && Math.Abs(p.Y - r.Top) <= handleTol) return HitZone.TopLeft;
        if (Math.Abs(p.X - r.Right) <= handleTol && Math.Abs(p.Y - r.Top) <= handleTol) return HitZone.TopRight;
        if (Math.Abs(p.X - r.Right) <= handleTol && Math.Abs(p.Y - r.Bottom) <= handleTol) return HitZone.BottomRight;
        if (Math.Abs(p.X - r.Left) <= handleTol && Math.Abs(p.Y - r.Bottom) <= handleTol) return HitZone.BottomLeft;

        // 2. Check 4 edge midpoints
        double midX = r.Left + r.Width / 2.0;
        double midY = r.Top + r.Height / 2.0;
        if (Math.Abs(p.X - midX) <= handleTol && Math.Abs(p.Y - r.Top) <= handleTol) return HitZone.Top;
        if (Math.Abs(p.X - r.Right) <= handleTol && Math.Abs(p.Y - midY) <= handleTol) return HitZone.Right;
        if (Math.Abs(p.X - midX) <= handleTol && Math.Abs(p.Y - r.Bottom) <= handleTol) return HitZone.Bottom;
        if (Math.Abs(p.X - r.Left) <= handleTol && Math.Abs(p.Y - midY) <= handleTol) return HitZone.Left;

        // 3. Check continuous edges
        if (Math.Abs(p.Y - r.Top) <= edgeTol && p.X >= r.Left && p.X <= r.Right) return HitZone.Top;
        if (Math.Abs(p.Y - r.Bottom) <= edgeTol && p.X >= r.Left && p.X <= r.Right) return HitZone.Bottom;
        if (Math.Abs(p.X - r.Left) <= edgeTol && p.Y >= r.Top && p.Y <= r.Bottom) return HitZone.Left;
        if (Math.Abs(p.X - r.Right) <= edgeTol && p.Y >= r.Top && p.Y <= r.Bottom) return HitZone.Right;

        // 4. Inside selection
        if (r.Contains(p)) return HitZone.Inside;

        return HitZone.None;
    }

    private Cursor GetCursorForHitZone(HitZone zone)
    {
        return zone switch
        {
            HitZone.TopLeft or HitZone.BottomRight => Cursors.SizeNWSE,
            HitZone.TopRight or HitZone.BottomLeft => Cursors.SizeNESW,
            HitZone.Top or HitZone.Bottom => Cursors.SizeNS,
            HitZone.Left or HitZone.Right => Cursors.SizeWE,
            HitZone.Inside => _currentTool switch
            {
                ToolType.Select => Cursors.SizeAll,
                ToolType.Text => Cursors.IBeam,
                _ => Cursors.Cross
            },
            _ => Cursors.Cross
        };
    }

    private void UpdateVisualCursor(Point pos)
    {
        if (_cursorBitmap == null) return;

        var settings = SettingsService.Instance.Current;
        _cachedCrosshairMask ??= CrosshairService.BuildMask(settings);

        // O pixel central da mira (índice GridCenter) fica exatamente no pixel sob o cursor.
        // Math.Floor (e não Round) evita deslocar a mira para o pixel vizinho quando a posição é fracionária.
        VisualCursorIndicator.RenderTransform = Transform.Identity;
        Canvas.SetLeft(VisualCursorIndicator, Math.Floor(pos.X) - CrosshairService.GridCenter);
        Canvas.SetTop(VisualCursorIndicator, Math.Floor(pos.Y) - CrosshairService.GridCenter);

        CursorOuterPath.Visibility = Visibility.Collapsed;
        CursorInnerPath.Visibility = Visibility.Collapsed;
        CursorInvertImage.Visibility = Visibility.Visible;

        bool hasSelection = !_selectionRect.IsEmpty;
        int baseX = (int)Math.Floor(pos.X);
        int baseY = (int)Math.Floor(pos.Y);

        CrosshairService.RenderToBuffer(
            _cursorPixels,
            _cachedCrosshairMask,
            settings,
            _cachedScreenPixels,
            _cachedBitmapWidth,
            _cachedBitmapHeight,
            baseX,
            baseY,
            _selectionRect,
            hasSelection);

        _cursorBitmap.WritePixels(_cursorDirtyRect, _cursorPixels, CrosshairService.GridSize * sizeof(int), 0);
    }

    #endregion

    #region Mouse Events on Overlay Canvas

    private void OverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        CloseAllFlyouts();
        Point pos = e.GetPosition(OverlayCanvas);

        if (_selectionRect.IsEmpty)
        {
            // Start creating selection
            _mode = InteractionMode.CreatingSelection;
            _startPoint = pos;
            _selectionRect = new Rect(pos, pos);
            UpdateSelectionVisuals();
            DrawingToolbar.Visibility = Visibility.Collapsed;
            ActionsToolbar.Visibility = Visibility.Collapsed;
            OverlayCanvas.CaptureMouse();
            return;
        }

        var hitZone = GetHitZone(pos);

        // If clicking on resize handles or edges -> RESIZE
        if (hitZone is HitZone.TopLeft or HitZone.Top or HitZone.TopRight or HitZone.Right
            or HitZone.BottomRight or HitZone.Bottom or HitZone.BottomLeft or HitZone.Left)
        {
            _mode = InteractionMode.ResizingSelection;
            _currentResizeZone = hitZone;
            _startPoint = pos;
            _initialSelectionRect = _selectionRect;
            OverlayCanvas.CaptureMouse();
        }
        // If clicking inside selection
        else if (hitZone == HitZone.Inside)
        {
            if (_currentTool == ToolType.Select)
            {
                // MOVE SELECTION
                _mode = InteractionMode.MovingSelection;
                _startPoint = pos;
                _initialSelectionRect = _selectionRect;
                OverlayCanvas.CaptureMouse();
            }
            else if (_currentTool == ToolType.Text)
            {
                ShowTextInput(pos);
            }
            else if (_currentTool == ToolType.StepMarker)
            {
                SaveUndoState();
                Point rel = ToRelativePoint(pos);
                _annotations.Add(new AnnotationItem
                {
                    Type = ToolType.StepMarker,
                    StartPoint = rel,
                    EndPoint = rel,
                    Color = _currentColor,
                    MarkerStyle = _stepMarkerStyle,
                    MarkerNumber = _nextStepMarkerIndex,
                    MarkerText = AnnotationItem.FormatMarkerText(_stepMarkerStyle, _nextStepMarkerIndex),
                    MarkerTextColor = Colors.White,
                    MarkerSize = _stepMarkerSize,
                    MarkerHasFill = true,
                    MarkerHasBorder = true
                });
                _nextStepMarkerIndex++;
                RedrawAllAnnotations();
            }
            else
            {
                // DRAW ANNOTATION
                _mode = InteractionMode.DrawingAnnotation;
                _startPoint = pos;
                SaveUndoState();

                Point rel = ToRelativePoint(pos);
                _currentAnnotation = new AnnotationItem
                {
                    Type = _currentTool,
                    StartPoint = rel,
                    EndPoint = rel,
                    Color = _currentColor,
                    StrokeThickness = _currentStrokeThickness,
                    ObfuscationStyle = _currentObfuscationStyle,
                    ObfuscationIntensity = _currentObfuscationIntensity,
                    Points = new List<Point> { rel }
                };
                OverlayCanvas.CaptureMouse();
            }
        }
        else
        {
            // Clicked outside selection -> start new selection
            _mode = InteractionMode.CreatingSelection;
            _startPoint = pos;
            _selectionRect = new Rect(pos, pos);
            _annotations.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            _nextStepMarkerIndex = 1;
            AnnotationCanvas.Children.Clear();
            UpdateSelectionVisuals();
            DrawingToolbar.Visibility = Visibility.Collapsed;
            ActionsToolbar.Visibility = Visibility.Collapsed;
            OverlayCanvas.CaptureMouse();
        }
    }

    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        Point pos = e.GetPosition(OverlayCanvas);

        bool isOverToolbar = false;
        if (DrawingToolbar.Visibility == Visibility.Visible)
        {
            double dLeft = Canvas.GetLeft(DrawingToolbar);
            double dTop = Canvas.GetTop(DrawingToolbar);
            double dWidth = DrawingToolbar.ActualWidth > 0 ? DrawingToolbar.ActualWidth : DrawingToolbar.DesiredSize.Width;
            double dHeight = DrawingToolbar.ActualHeight > 0 ? DrawingToolbar.ActualHeight : DrawingToolbar.DesiredSize.Height;
            if (pos.X >= dLeft && pos.X <= dLeft + dWidth && pos.Y >= dTop && pos.Y <= dTop + dHeight)
            {
                isOverToolbar = true;
            }
        }
        if (!isOverToolbar && ActionsToolbar.Visibility == Visibility.Visible)
        {
            double aLeft = Canvas.GetLeft(ActionsToolbar);
            double aTop = Canvas.GetTop(ActionsToolbar);
            double aWidth = ActionsToolbar.ActualWidth > 0 ? ActionsToolbar.ActualWidth : ActionsToolbar.DesiredSize.Width;
            double aHeight = ActionsToolbar.ActualHeight > 0 ? ActionsToolbar.ActualHeight : ActionsToolbar.DesiredSize.Height;
            if (pos.X >= aLeft && pos.X <= aLeft + aWidth && pos.Y >= aTop && pos.Y <= aTop + aHeight)
            {
                isOverToolbar = true;
            }
        }

        var hitZone = GetHitZone(pos);
        bool isResizeHandle = hitZone is HitZone.TopLeft or HitZone.Top or HitZone.TopRight or HitZone.Right
            or HitZone.BottomRight or HitZone.Bottom or HitZone.BottomLeft or HitZone.Left;

        if (isOverToolbar)
        {
            VisualCursorIndicator.Visibility = Visibility.Collapsed;
            OverlayCanvas.Cursor = Cursors.Arrow;
        }
        else if (_mode == InteractionMode.ResizingSelection || isResizeHandle)
        {
            VisualCursorIndicator.Visibility = Visibility.Collapsed;
            OverlayCanvas.Cursor = GetCursorForHitZone(_mode == InteractionMode.ResizingSelection ? _currentResizeZone : hitZone);
        }
        else if (_mode == InteractionMode.MovingSelection || (_mode == InteractionMode.None && hitZone == HitZone.Inside && _currentTool == ToolType.Select))
        {
            VisualCursorIndicator.Visibility = Visibility.Collapsed;
            OverlayCanvas.Cursor = Cursors.SizeAll;
        }
        else if (_mode == InteractionMode.None && hitZone == HitZone.Inside && _currentTool == ToolType.Text)
        {
            VisualCursorIndicator.Visibility = Visibility.Collapsed;
            OverlayCanvas.Cursor = Cursors.IBeam;
        }
        else
        {
            VisualCursorIndicator.Visibility = Visibility.Visible;
            OverlayCanvas.Cursor = Cursors.None;
            UpdateVisualCursor(pos);
        }

        switch (_mode)
        {
            case InteractionMode.CreatingSelection:
            {
                double x = Math.Min(_startPoint.X, pos.X);
                double y = Math.Min(_startPoint.Y, pos.Y);
                double w = Math.Abs(_startPoint.X - pos.X);
                double h = Math.Abs(_startPoint.Y - pos.Y);

                x = Math.Clamp(x, 0, ActualWidth);
                y = Math.Clamp(y, 0, ActualHeight);
                w = Math.Min(w, ActualWidth - x);
                h = Math.Min(h, ActualHeight - y);

                _selectionRect = new Rect(x, y, w, h);
                UpdateSelectionVisuals();
                break;
            }

            case InteractionMode.MovingSelection:
            {
                double dx = pos.X - _startPoint.X;
                double dy = pos.Y - _startPoint.Y;

                double newLeft = _initialSelectionRect.X + dx;
                double newTop = _initialSelectionRect.Y + dy;

                newLeft = Math.Clamp(newLeft, 0, ActualWidth - _initialSelectionRect.Width);
                newTop = Math.Clamp(newTop, 0, ActualHeight - _initialSelectionRect.Height);

                _selectionRect = new Rect(newLeft, newTop, _initialSelectionRect.Width, _initialSelectionRect.Height);
                UpdateSelectionVisuals();
                PositionToolbar();
                break;
            }

            case InteractionMode.ResizingSelection:
            {
                double left = _initialSelectionRect.Left;
                double top = _initialSelectionRect.Top;
                double right = _initialSelectionRect.Right;
                double bottom = _initialSelectionRect.Bottom;

                if (_currentResizeZone is HitZone.TopLeft or HitZone.Left or HitZone.BottomLeft)
                {
                    left = Math.Min(pos.X, right - 15);
                }
                if (_currentResizeZone is HitZone.TopRight or HitZone.Right or HitZone.BottomRight)
                {
                    right = Math.Max(pos.X, left + 15);
                }
                if (_currentResizeZone is HitZone.TopLeft or HitZone.Top or HitZone.TopRight)
                {
                    top = Math.Min(pos.Y, bottom - 15);
                }
                if (_currentResizeZone is HitZone.BottomLeft or HitZone.Bottom or HitZone.BottomRight)
                {
                    bottom = Math.Max(pos.Y, top + 15);
                }

                left = Math.Clamp(left, 0, ActualWidth);
                right = Math.Clamp(right, 0, ActualWidth);
                top = Math.Clamp(top, 0, ActualHeight);
                bottom = Math.Clamp(bottom, 0, ActualHeight);

                _selectionRect = new Rect(left, top, Math.Max(15, right - left), Math.Max(15, bottom - top));
                UpdateSelectionVisuals();
                PositionToolbar();
                break;
            }

            case InteractionMode.DrawingAnnotation:
            {
                if (_currentAnnotation != null)
                {
                    Point relPos = ToRelativePoint(pos);
                    _currentAnnotation.EndPoint = relPos;

                    if (_currentTool is ToolType.Pen or ToolType.Highlighter)
                    {
                        _currentAnnotation.Points.Add(relPos);
                    }

                    RenderTempPreview(_currentAnnotation);
                }
                break;
            }

            case InteractionMode.None:
            default:
                break;
        }
    }

    private void OverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        OverlayCanvas.ReleaseMouseCapture();

        switch (_mode)
        {
            case InteractionMode.CreatingSelection:
            {
                if (_selectionRect.Width < 10 || _selectionRect.Height < 10)
                {
                    _selectionRect = Rect.Empty;
                    UpdateSelectionVisuals();
                    DrawingToolbar.Visibility = Visibility.Collapsed;
                    ActionsToolbar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    DrawingToolbar.Visibility = Visibility.Visible;
                    ActionsToolbar.Visibility = Visibility.Visible;
                    AnnotationCanvas.Visibility = Visibility.Visible;
                    UpdateSelectionVisuals();
                    PositionToolbar();
                }
                break;
            }

            case InteractionMode.MovingSelection:
            case InteractionMode.ResizingSelection:
            {
                DrawingToolbar.Visibility = Visibility.Visible;
                ActionsToolbar.Visibility = Visibility.Visible;
                UpdateSelectionVisuals();
                PositionToolbar();
                break;
            }

            case InteractionMode.DrawingAnnotation:
            {
                if (_currentAnnotation != null)
                {
                    _annotations.Add(_currentAnnotation);
                    _currentAnnotation = null;

                    if (_tempPreviewShape != null)
                    {
                        AnnotationCanvas.Children.Remove(_tempPreviewShape);
                        _tempPreviewShape = null;
                    }

                    RedrawAllAnnotations();
                }
                break;
            }
        }

        _mode = InteractionMode.None;
    }

    #endregion

    #region Visuals, Handles & Responsive Toolbar Positioning

    private void UpdateSelectionVisuals()
    {
        if (_selectionRect.IsEmpty)
        {
            SelectionGeometry.Rect = Rect.Empty;
            SelectionBorder.Visibility = Visibility.Collapsed;
            DimensionBadge.Visibility = Visibility.Collapsed;
            SetHandlesVisibility(Visibility.Collapsed);
            return;
        }

        SelectionGeometry.Rect = _selectionRect;

        SelectionBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionBorder, _selectionRect.Left);
        Canvas.SetTop(SelectionBorder, _selectionRect.Top);
        SelectionBorder.Width = _selectionRect.Width;
        SelectionBorder.Height = _selectionRect.Height;

        // Position Dimension Badge
        DimensionBadge.Visibility = Visibility.Visible;
        DimensionText.Text = $"{(int)_selectionRect.Width} × {(int)_selectionRect.Height} px";

        double badgeLeft = _selectionRect.Left;
        double badgeTop = _selectionRect.Top - 26;
        if (badgeTop < 8) badgeTop = _selectionRect.Top + 8;
        Canvas.SetLeft(DimensionBadge, Math.Max(6, badgeLeft));
        Canvas.SetTop(DimensionBadge, Math.Max(6, badgeTop));

        // Position 8 Handles
        UpdateHandlesPosition();
        SetHandlesVisibility(Visibility.Visible);

        // Position AnnotationCanvas
        Canvas.SetLeft(AnnotationCanvas, _selectionRect.Left);
        Canvas.SetTop(AnnotationCanvas, _selectionRect.Top);
        AnnotationCanvas.Width = _selectionRect.Width;
        AnnotationCanvas.Height = _selectionRect.Height;
        AnnotationCanvas.Clip = new RectangleGeometry(new Rect(0, 0, _selectionRect.Width, _selectionRect.Height));
    }

    private void UpdateHandlesPosition()
    {
        var r = _selectionRect;
        const double half = 4.0;

        Canvas.SetLeft(HandleNW, r.Left - half);
        Canvas.SetTop(HandleNW, r.Top - half);

        Canvas.SetLeft(HandleN, r.Left + r.Width / 2.0 - half);
        Canvas.SetTop(HandleN, r.Top - half);

        Canvas.SetLeft(HandleNE, r.Right - half);
        Canvas.SetTop(HandleNE, r.Top - half);

        Canvas.SetLeft(HandleE, r.Right - half);
        Canvas.SetTop(HandleE, r.Top + r.Height / 2.0 - half);

        Canvas.SetLeft(HandleSE, r.Right - half);
        Canvas.SetTop(HandleSE, r.Bottom - half);

        Canvas.SetLeft(HandleS, r.Left + r.Width / 2.0 - half);
        Canvas.SetTop(HandleS, r.Bottom - half);

        Canvas.SetLeft(HandleSW, r.Left - half);
        Canvas.SetTop(HandleSW, r.Bottom - half);

        Canvas.SetLeft(HandleW, r.Left - half);
        Canvas.SetTop(HandleW, r.Top + r.Height / 2.0 - half);
    }

    private void SetHandlesVisibility(Visibility v)
    {
        HandleNW.Visibility = v;
        HandleN.Visibility = v;
        HandleNE.Visibility = v;
        HandleE.Visibility = v;
        HandleSE.Visibility = v;
        HandleS.Visibility = v;
        HandleSW.Visibility = v;
        HandleW.Visibility = v;
    }

    private void PositionToolbar()
    {
        if (_selectionRect.IsEmpty || _selectionRect.Width < 10 || _selectionRect.Height < 10)
        {
            DrawingToolbar.Visibility = Visibility.Collapsed;
            ActionsToolbar.Visibility = Visibility.Collapsed;
            return;
        }

        // Determine which physical monitor contains the center of the selection
        int absCenterX = (int)(_selectionRect.X + _virtualBounds.Left + _selectionRect.Width / 2);
        int absCenterY = (int)(_selectionRect.Y + _virtualBounds.Top + _selectionRect.Height / 2);
        var targetScreen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(absCenterX, absCenterY));

        // Convert monitor boundaries to overlay canvas coordinates
        double monLeft = targetScreen.Bounds.Left - _virtualBounds.Left;
        double monTop = targetScreen.Bounds.Top - _virtualBounds.Top;
        double monRight = monLeft + targetScreen.Bounds.Width;
        double monBottom = monTop + targetScreen.Bounds.Height;

        const double margin = 12.0;

        DrawingToolbar.Visibility = Visibility.Visible;
        ActionsToolbar.Visibility = Visibility.Visible;

        // Measure DesiredSize
        DrawingToolbar.Measure(new Size(monRight - monLeft, monBottom - monTop));
        double drawW = Math.Max(260, DrawingToolbar.DesiredSize.Width);
        double drawH = Math.Max(36, DrawingToolbar.DesiredSize.Height);

        ActionsToolbar.Measure(new Size(monRight - monLeft, monBottom - monTop));
        double actW = Math.Max(42, ActionsToolbar.DesiredSize.Width);
        double actH = Math.Max(180, ActionsToolbar.DesiredSize.Height);

        double spaceBelow = monBottom - _selectionRect.Bottom;
        double spaceAbove = _selectionRect.Top - monTop;
        double spaceRight = monRight - _selectionRect.Right;
        double spaceLeft = _selectionRect.Left - monLeft;

        // 1. Position ActionsToolbar (Vertical - preferred RIGHT of selection)
        double actTop;
        double actLeft;

        if (spaceRight >= actW + margin)
        {
            actLeft = _selectionRect.Right + margin;
            actTop = _selectionRect.Top;
        }
        else if (spaceLeft >= actW + margin)
        {
            actLeft = _selectionRect.Left - actW - margin;
            actTop = _selectionRect.Top;
        }
        else if (_selectionRect.Width >= actW + margin * 2 && _selectionRect.Height >= actH + margin * 2)
        {
            // Dock inside selection right edge with handle safety margin
            actLeft = _selectionRect.Right - actW - margin;
            actTop = _selectionRect.Top + margin;
        }
        else
        {
            actLeft = Math.Clamp(_selectionRect.Right + margin, monLeft + margin, monRight - actW - margin);
            actTop = Math.Clamp(_selectionRect.Top, monTop + margin, monBottom - actH - margin);
        }

        // Constrain vertically within monitor
        if (actTop + actH > monBottom - margin) actTop = monBottom - actH - margin;
        if (actTop < monTop + margin) actTop = monTop + margin;
        if (actLeft + actW > monRight - margin) actLeft = monRight - actW - margin;
        if (actLeft < monLeft + margin) actLeft = monLeft + margin;

        Canvas.SetLeft(ActionsToolbar, actLeft);
        Canvas.SetTop(ActionsToolbar, actTop);

        // 2. Position DrawingToolbar (Horizontal - preferred BELOW selection)
        double drawTop;
        double drawLeft;

        if (spaceBelow >= drawH + margin)
        {
            drawTop = _selectionRect.Bottom + margin;
            drawLeft = _selectionRect.Left;
        }
        else if (spaceAbove >= drawH + margin)
        {
            drawTop = _selectionRect.Top - drawH - margin;
            drawLeft = _selectionRect.Left;
        }
        else if (_selectionRect.Height >= drawH + margin * 2 && _selectionRect.Width >= drawW + margin * 2)
        {
            // Inside selection near bottom with handle safety margin
            drawTop = _selectionRect.Bottom - drawH - margin;
            drawLeft = _selectionRect.Left + margin;
        }
        else
        {
            drawTop = Math.Clamp(_selectionRect.Bottom + margin, monTop + margin, monBottom - drawH - margin);
            drawLeft = Math.Clamp(_selectionRect.Left, monLeft + margin, monRight - drawW - margin);
        }

        // Constrain horizontally within monitor
        if (drawLeft + drawW > monRight - margin) drawLeft = monRight - drawW - margin;
        if (drawLeft < monLeft + margin) drawLeft = monLeft + margin;
        if (drawTop + drawH > monBottom - margin) drawTop = monBottom - drawH - margin;
        if (drawTop < monTop + margin) drawTop = monTop + margin;

        // Collision resolution between the two bars:
        var actRect = new Rect(actLeft, actTop, actW, actH);
        var drawRect = new Rect(drawLeft, drawTop, drawW, drawH);
        if (actRect.IntersectsWith(drawRect))
        {
            if (actLeft >= _selectionRect.Right - margin)
            {
                if (drawLeft + drawW > actLeft - 8)
                {
                    drawLeft = Math.Max(monLeft + margin, actLeft - drawW - 8);
                }
            }
            else if (actLeft <= _selectionRect.Left + margin)
            {
                if (drawLeft < actLeft + actW + 8)
                {
                    drawLeft = Math.Min(monRight - drawW - margin, actLeft + actW + 8);
                }
            }

            drawRect = new Rect(drawLeft, drawTop, drawW, drawH);
            if (actRect.IntersectsWith(drawRect))
            {
                if (drawTop >= _selectionRect.Bottom - margin)
                {
                    drawTop = Math.Min(monBottom - drawH - margin, actTop + actH + 8);
                }
                else if (drawTop <= _selectionRect.Top + margin)
                {
                    drawTop = Math.Max(monTop + margin, actTop - drawH - 8);
                }
            }
        }

        Canvas.SetLeft(DrawingToolbar, drawLeft);
        Canvas.SetTop(DrawingToolbar, drawTop);

        // Dynamic Popup Orientation: open downwards when toolbar is near screen top, upwards when near bottom
        bool openDownwards = (drawTop - monTop < 220);
        var popupPlacement = openDownwards ? System.Windows.Controls.Primitives.PlacementMode.Bottom : System.Windows.Controls.Primitives.PlacementMode.Top;
        double popupVOffset = openDownwards ? 4 : -4;

        if (PopupPenMenu != null)
        {
            PopupPenMenu.Placement = popupPlacement;
            PopupPenMenu.VerticalOffset = popupVOffset;
        }
        if (PopupShapesMenu != null)
        {
            PopupShapesMenu.Placement = popupPlacement;
            PopupShapesMenu.VerticalOffset = popupVOffset;
        }
        if (PopupStepMarkerMenu != null)
        {
            PopupStepMarkerMenu.Placement = popupPlacement;
            PopupStepMarkerMenu.VerticalOffset = popupVOffset;
        }
        if (PopupBlurMenu != null)
        {
            PopupBlurMenu.Placement = popupPlacement;
            PopupBlurMenu.VerticalOffset = popupVOffset;
        }
        if (PopupColorMenu != null)
        {
            PopupColorMenu.Placement = popupPlacement;
            PopupColorMenu.VerticalOffset = popupVOffset;
        }
    }

    private Point ToRelativePoint(Point screenPoint)
    {
        return new Point(
            Math.Max(0, Math.Min(_selectionRect.Width, screenPoint.X - _selectionRect.Left)),
            Math.Max(0, Math.Min(_selectionRect.Height, screenPoint.Y - _selectionRect.Top))
        );
    }

    #endregion

    #region Annotations Rendering

    private void RenderTempPreview(AnnotationItem item)
    {
        if (_tempPreviewShape != null)
        {
            AnnotationCanvas.Children.Remove(_tempPreviewShape);
            _tempPreviewShape = null;
        }

        var shape = CreateShapeElement(item);
        if (shape != null)
        {
            _tempPreviewShape = shape;
            AnnotationCanvas.Children.Add(_tempPreviewShape);
        }
    }

    private void RedrawAllAnnotations()
    {
        AnnotationCanvas.Children.Clear();
        foreach (var ann in _annotations)
        {
            var shape = CreateShapeElement(ann);
            if (shape != null)
            {
                AnnotationCanvas.Children.Add(shape);
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

                var r = new System.Windows.Shapes.Rectangle
                {
                    Stroke = brush,
                    StrokeThickness = item.StrokeThickness,
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

                var el = new System.Windows.Shapes.Ellipse
                {
                    Stroke = brush,
                    StrokeThickness = item.StrokeThickness,
                    Width = ew,
                    Height = eh
                };
                Canvas.SetLeft(el, ex);
                Canvas.SetTop(el, ey);
                return el;

            case ToolType.Pen:
                if (item.Points.Count < 2) return null;
                var poly = new Polyline
                {
                    Stroke = brush,
                    StrokeThickness = item.StrokeThickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                foreach (var p in item.Points) poly.Points.Add(p);
                return poly;

            case ToolType.Highlighter:
                if (item.Points.Count < 2) return null;
                var hlBrush = new SolidColorBrush(Color.FromArgb(90, item.Color.R, item.Color.G, item.Color.B));
                var hl = new Polyline
                {
                    Stroke = hlBrush,
                    StrokeThickness = Math.Max(item.StrokeThickness * 3, 16),
                    StrokeLineJoin = PenLineJoin.Bevel
                };
                foreach (var p in item.Points) hl.Points.Add(p);
                return hl;

            case ToolType.Text:
                var tb = new TextBlock
                {
                    Text = item.Text,
                    Foreground = brush,
                    FontSize = item.FontSize,
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

                if (bw > 2 && bh > 2 && _fullBitmap != null)
                {
                    int cropX = (int)(_selectionRect.X + bx);
                    int cropY = (int)(_selectionRect.Y + by);
                    var cropRect = new System.Drawing.Rectangle(cropX, cropY, (int)bw, (int)bh);
                    int intensity = item.ObfuscationIntensity > 0 ? item.ObfuscationIntensity : _currentObfuscationIntensity;
                    var bs = ExportService.CreateObfuscatedRegionBitmap(_fullBitmap, cropRect, item.ObfuscationStyle, intensity);
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

        Point pArrow1 = new Point(
            p2.X - arrowHeadLength * Math.Cos(theta - arrowHeadAngle),
            p2.Y - arrowHeadLength * Math.Sin(theta - arrowHeadAngle));

        Point pArrow2 = new Point(
            p2.X - arrowHeadLength * Math.Cos(theta + arrowHeadAngle),
            p2.Y - arrowHeadLength * Math.Sin(theta + arrowHeadAngle));

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

    #region Text Input Handling

    private void ShowTextInput(Point clickPos)
    {
        InlineTextInput.Text = string.Empty;
        Canvas.SetLeft(InlineTextInput, clickPos.X);
        Canvas.SetTop(InlineTextInput, clickPos.Y);
        InlineTextInput.Visibility = Visibility.Visible;
        InlineTextInput.Focus();
    }

    private void InlineTextInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitInlineText();
        }
        else if (e.Key == Key.Escape)
        {
            InlineTextInput.Visibility = Visibility.Collapsed;
        }
    }

    private void InlineTextInput_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitInlineText();
    }

    private void CommitInlineText()
    {
        if (InlineTextInput.Visibility != Visibility.Visible) return;

        string text = InlineTextInput.Text.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            SaveUndoState();
            double x = Canvas.GetLeft(InlineTextInput);
            double y = Canvas.GetTop(InlineTextInput);

            _annotations.Add(new AnnotationItem
            {
                Type = ToolType.Text,
                StartPoint = ToRelativePoint(new Point(x, y)),
                Text = text,
                Color = _currentColor,
                FontSize = 16
            });

            RedrawAllAnnotations();
        }

        InlineTextInput.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Undo / Redo

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

    #endregion

    #region Flyout Management

    private void ShowFlyout(System.Windows.Controls.Primitives.Popup popup)
    {
        _flyoutTimer?.Stop();
        if (_activeFlyout != null && _activeFlyout != popup)
        {
            _activeFlyout.IsOpen = false;
        }
        _activeFlyout = popup;
        popup.IsOpen = true;
    }

    private void ScheduleCloseFlyout(System.Windows.Controls.Primitives.Popup popup)
    {
        if (_activeFlyout == popup)
        {
            _flyoutTimer?.Stop();
            _flyoutTimer?.Start();
        }
    }

    private void CancelCloseFlyout(System.Windows.Controls.Primitives.Popup popup)
    {
        if (_activeFlyout == popup)
        {
            _flyoutTimer?.Stop();
        }
    }

    private void CloseAllFlyouts()
    {
        _flyoutTimer?.Stop();
        if (_activeFlyout != null)
        {
            _activeFlyout.IsOpen = false;
            _activeFlyout = null;
        }
        if (PopupPenMenu != null) PopupPenMenu.IsOpen = false;
        if (PopupShapesMenu != null) PopupShapesMenu.IsOpen = false;
        if (PopupStepMarkerMenu != null) PopupStepMarkerMenu.IsOpen = false;
        if (PopupBlurMenu != null) PopupBlurMenu.IsOpen = false;
        if (PopupColorMenu != null) PopupColorMenu.IsOpen = false;
    }

    #endregion

    #region Toolbar Handlers & Tool / Color Selection

    private void SelectTool(ToolType tool)
    {
        _currentTool = tool;
        if (_toolColors.TryGetValue(tool, out var c))
        {
            _currentColor = c;
        }
        if (_toolThicknesses.TryGetValue(tool, out var th))
        {
            if (tool == ToolType.StepMarker)
            {
                _stepMarkerSize = th;
                if (TxtMarkerSizeValue != null) TxtMarkerSizeValue.Text = $"{(int)th} px";
            }
            else
            {
                _currentStrokeThickness = th;
                if (SliderPenThickness != null) SliderPenThickness.Value = th;
                if (TxtPenThicknessValue != null) TxtPenThicknessValue.Text = $"{(int)th} px";
            }
        }
        UpdateToolButtonStates();
        UpdateColorButtonStates();
    }

    private void SetStrokeThickness(double th)
    {
        _currentStrokeThickness = th;
        _toolThicknesses[_currentTool] = th;
        if (SliderPenThickness != null && Math.Abs(SliderPenThickness.Value - th) > 0.5)
        {
            SliderPenThickness.Value = th;
        }
        if (TxtPenThicknessValue != null)
        {
            TxtPenThicknessValue.Text = $"{(int)th} px";
        }
    }

    private void SliderPenThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _currentStrokeThickness = Math.Round(e.NewValue);
        _toolThicknesses[_currentTool] = _currentStrokeThickness;
        if (TxtPenThicknessValue != null)
        {
            TxtPenThicknessValue.Text = $"{_currentStrokeThickness} px";
        }
    }

    // Pen Flyout Group Handlers
    private void BtnPenGroup_Click(object sender, RoutedEventArgs e)
    {
        bool isPenActive = _currentTool is ToolType.Pen or ToolType.Highlighter;
        if (!isPenActive)
        {
            var target = TxtActivePenIcon.Text == "🖍" ? ToolType.Highlighter : ToolType.Pen;
            SelectTool(target);
            CloseAllFlyouts();
        }
        else
        {
            if (PopupPenMenu.IsOpen)
                CloseAllFlyouts();
            else
                ShowFlyout(PopupPenMenu);
        }
    }

    private void BtnPenGroup_MouseEnter(object sender, MouseEventArgs e) => ShowFlyout(PopupPenMenu);
    private void BtnPenGroup_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupPenMenu);
    private void PopupPenMenu_MouseEnter(object sender, MouseEventArgs e) => CancelCloseFlyout(PopupPenMenu);
    private void PopupPenMenu_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupPenMenu);

    private void BtnSelectPenTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && Enum.TryParse<ToolType>(tagStr, out var tool))
        {
            SelectTool(tool);
            CloseAllFlyouts();
        }
    }

    private void BtnPenThickness_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && double.TryParse(tagStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var th))
        {
            SetStrokeThickness(th);
            CloseAllFlyouts();
        }
    }

    // Shapes Flyout Group Handlers
    private void BtnShapesGroup_Click(object sender, RoutedEventArgs e)
    {
        bool isShapeActive = _currentTool is ToolType.Rectangle or ToolType.Ellipse or ToolType.Line;
        if (!isShapeActive)
        {
            var target = TxtActiveShapeIcon.Text switch
            {
                "◯" => ToolType.Ellipse,
                "╱" => ToolType.Line,
                _ => ToolType.Rectangle
            };
            SelectTool(target);
            CloseAllFlyouts();
        }
        else
        {
            if (PopupShapesMenu.IsOpen)
                CloseAllFlyouts();
            else
                ShowFlyout(PopupShapesMenu);
        }
    }

    private void BtnShapesGroup_MouseEnter(object sender, MouseEventArgs e) => ShowFlyout(PopupShapesMenu);
    private void BtnShapesGroup_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupShapesMenu);
    private void PopupShapesMenu_MouseEnter(object sender, MouseEventArgs e) => CancelCloseFlyout(PopupShapesMenu);
    private void PopupShapesMenu_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupShapesMenu);

    private void BtnSelectShapeTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && Enum.TryParse<ToolType>(tagStr, out var tool))
        {
            SelectTool(tool);
            CloseAllFlyouts();
        }
    }

    // StepMarker Flyout Group Handlers
    private void BtnStepMarkerGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTool != ToolType.StepMarker)
        {
            SelectTool(ToolType.StepMarker);
            CloseAllFlyouts();
        }
        else
        {
            if (PopupStepMarkerMenu.IsOpen)
                CloseAllFlyouts();
            else
                ShowFlyout(PopupStepMarkerMenu);
        }
    }

    private void BtnStepMarkerGroup_MouseEnter(object sender, MouseEventArgs e) => ShowFlyout(PopupStepMarkerMenu);
    private void BtnStepMarkerGroup_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupStepMarkerMenu);
    private void PopupStepMarkerMenu_MouseEnter(object sender, MouseEventArgs e) => CancelCloseFlyout(PopupStepMarkerMenu);
    private void PopupStepMarkerMenu_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupStepMarkerMenu);

    private void BtnMarkerStyle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && Enum.TryParse<StepMarkerStyle>(tagStr, out var style))
        {
            _stepMarkerStyle = style;
            SelectTool(ToolType.StepMarker);
            CloseAllFlyouts();
        }
    }

    private void BtnMarkerSizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && double.TryParse(tagStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sz))
        {
            _stepMarkerSize = sz;
            _toolThicknesses[ToolType.StepMarker] = sz;
            if (TxtMarkerSizeValue != null)
            {
                TxtMarkerSizeValue.Text = $"{(int)sz} px";
            }
            CloseAllFlyouts();
        }
    }

    private void BtnRestartMarkerSequence_Click(object sender, RoutedEventArgs e)
    {
        _nextStepMarkerIndex = 1;
        CloseAllFlyouts();
    }

    // Blur / Obfuscate Flyout Group Handlers
    private void BtnBlurGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTool != ToolType.Blur)
        {
            _currentTool = ToolType.Blur;
            UpdateToolButtonStates();
            CloseAllFlyouts();
        }
        else
        {
            if (PopupBlurMenu.IsOpen)
                CloseAllFlyouts();
            else
                ShowFlyout(PopupBlurMenu);
        }
    }

    private void BtnBlurGroup_MouseEnter(object sender, MouseEventArgs e) => ShowFlyout(PopupBlurMenu);
    private void BtnBlurGroup_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupBlurMenu);
    private void PopupBlurMenu_MouseEnter(object sender, MouseEventArgs e) => CancelCloseFlyout(PopupBlurMenu);
    private void PopupBlurMenu_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupBlurMenu);

    private void BtnSelectBlurMode_Click(object sender, RoutedEventArgs e)
    {
        _currentObfuscationStyle = ObfuscationStyle.Blur;
        SettingsService.Instance.Current.DefaultObfuscationStyle = ObfuscationStyle.Blur;
        SettingsService.Instance.Save();
        _currentTool = ToolType.Blur;
        UpdateToolButtonStates();
        UpdateLastBlurAnnotation();
    }

    private void BtnSelectPixelateMode_Click(object sender, RoutedEventArgs e)
    {
        _currentObfuscationStyle = ObfuscationStyle.Pixelate;
        SettingsService.Instance.Current.DefaultObfuscationStyle = ObfuscationStyle.Pixelate;
        SettingsService.Instance.Save();
        _currentTool = ToolType.Blur;
        UpdateToolButtonStates();
        UpdateLastBlurAnnotation();
    }

    private void SliderBlurIntensity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _currentObfuscationIntensity = (int)e.NewValue;
        if (TxtBlurIntensityValue != null)
        {
            TxtBlurIntensityValue.Text = $"{_currentObfuscationIntensity} px";
        }
        SettingsService.Instance.Current.ObfuscationIntensity = _currentObfuscationIntensity;
        UpdateLastBlurAnnotation();
    }

    private void BtnBlurPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int val))
        {
            _currentObfuscationIntensity = val;
            if (SliderBlurIntensity != null)
            {
                SliderBlurIntensity.Value = val;
            }
            if (TxtBlurIntensityValue != null)
            {
                TxtBlurIntensityValue.Text = $"{val} px";
            }
            SettingsService.Instance.Current.ObfuscationIntensity = val;
            SettingsService.Instance.Save();
            UpdateLastBlurAnnotation();
        }
    }

    private void UpdateLastBlurAnnotation()
    {
        var last = _annotations.LastOrDefault(a => a.Type == ToolType.Blur);
        if (last != null)
        {
            last.ObfuscationStyle = _currentObfuscationStyle;
            last.ObfuscationIntensity = _currentObfuscationIntensity;
            RedrawAllAnnotations();
        }
    }

    // Color Flyout Group Handlers
    private void BtnColorGroup_Click(object sender, RoutedEventArgs e)
    {
        if (PopupColorMenu.IsOpen)
            CloseAllFlyouts();
        else
            ShowFlyout(PopupColorMenu);
    }

    private void BtnColorGroup_MouseEnter(object sender, MouseEventArgs e) => ShowFlyout(PopupColorMenu);
    private void BtnColorGroup_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupColorMenu);
    private void PopupColorMenu_MouseEnter(object sender, MouseEventArgs e) => CancelCloseFlyout(PopupColorMenu);
    private void PopupColorMenu_MouseLeave(object sender, MouseEventArgs e) => ScheduleCloseFlyout(PopupColorMenu);

    private void TxtColorPickerHex_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdatingColorPickerText || TxtColorPickerHex == null) return;
        string text = TxtColorPickerHex.Text.Trim();
        if (TryParseHexColor(text, out var color))
        {
            _currentColor = color;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            EllipseActiveColor.Fill = brush;
            if (EllipseColorPickerPreview != null) EllipseColorPickerPreview.Fill = brush;
            if (InlineTextInput != null && InlineTextInput.Visibility == Visibility.Visible)
            {
                InlineTextInput.Foreground = brush;
            }
        }
    }

    private void TxtColorPickerHex_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnApplyColorPickerHex_Click(sender, e);
            e.Handled = true;
        }
    }

    private void BtnApplyColorPickerHex_Click(object sender, RoutedEventArgs e)
    {
        if (TxtColorPickerHex == null) return;
        string text = TxtColorPickerHex.Text.Trim();
        if (TryParseHexColor(text, out var color))
        {
            SetAnnotationColor(color, closeFlyout: true);
        }
    }

    private static bool TryParseHexColor(string hex, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        hex = hex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        try
        {
            var converted = ColorConverter.ConvertFromString(hex);
            if (converted is Color c)
            {
                color = c;
                return true;
            }
        }
        catch { }
        return false;
    }

    private void SetAnnotationColor(Color color, bool closeFlyout = false)
    {
        _currentColor = color;
        _toolColors[_currentTool] = color;
        UpdateColorButtonStates();
        if (closeFlyout)
        {
            CloseAllFlyouts();
        }
    }

    private void BtnColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            if (TryParseHexColor(hex, out var color))
            {
                SetAnnotationColor(color, closeFlyout: true);
            }
        }
    }

    private void BtnCustomAnnotationColor_Click(object sender, RoutedEventArgs e)
    {
        CloseAllFlyouts();
        using var dlg = new System.Windows.Forms.ColorDialog();
        dlg.Color = System.Drawing.Color.FromArgb(_currentColor.R, _currentColor.G, _currentColor.B);
        dlg.FullOpen = true;
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SetAnnotationColor(Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B));
        }
    }

    private void BtnTool_Click(object sender, RoutedEventArgs e)
    {
        CloseAllFlyouts();
        if (sender is Button btn && btn.Tag is string tagStr)
        {
            if (Enum.TryParse<ToolType>(tagStr, out var selectedTool))
            {
                SelectTool(selectedTool);
            }
        }
    }

    private void UpdateToolButtonStates()
    {
        var buttons = new (Button?, ToolType)[]
        {
            (BtnToolSelect, ToolType.Select),
            (BtnToolPen, ToolType.Pen),
            (BtnToolHighlighter, ToolType.Highlighter),
            (BtnToolLine, ToolType.Line),
            (BtnToolArrow, ToolType.Arrow),
            (BtnToolRect, ToolType.Rectangle),
            (BtnToolEllipse, ToolType.Ellipse),
            (BtnToolText, ToolType.Text)
        };

        foreach (var (b, tool) in buttons)
        {
            if (b == null) continue;
            bool isActive = tool == _currentTool;
            if (isActive)
            {
                b.SetResourceReference(Button.BackgroundProperty, "AccentPrimaryBrush");
                b.SetResourceReference(Button.BorderBrushProperty, "AccentBorderBrush");
                b.BorderThickness = new Thickness(1);
                b.SetResourceReference(Button.ForegroundProperty, "AccentForegroundBrush");
            }
            else
            {
                b.Background = Brushes.Transparent;
                b.BorderBrush = Brushes.Transparent;
                b.BorderThickness = new Thickness(0);
                b.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            }
        }

        // Update Pen group button state & active icon
        bool isPenGroupActive = _currentTool is ToolType.Pen or ToolType.Highlighter;
        if (isPenGroupActive)
        {
            BtnPenGroup.SetResourceReference(Button.BackgroundProperty, "AccentPrimaryBrush");
            BtnPenGroup.SetResourceReference(Button.BorderBrushProperty, "AccentBorderBrush");
            BtnPenGroup.BorderThickness = new Thickness(1);
            BtnPenGroup.SetResourceReference(Button.ForegroundProperty, "AccentForegroundBrush");
            TxtActivePenIcon.SetResourceReference(TextBlock.ForegroundProperty, "AccentForegroundBrush");
        }
        else
        {
            BtnPenGroup.Background = Brushes.Transparent;
            BtnPenGroup.BorderBrush = Brushes.Transparent;
            BtnPenGroup.BorderThickness = new Thickness(0);
            BtnPenGroup.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            TxtActivePenIcon.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        }
        TxtActivePenIcon.Text = _currentTool == ToolType.Highlighter ? "🖍" : "✎";

        // Update Shapes group button state & active icon
        bool isShapeGroupActive = _currentTool is ToolType.Rectangle or ToolType.Ellipse or ToolType.Line;
        if (isShapeGroupActive)
        {
            BtnShapesGroup.SetResourceReference(Button.BackgroundProperty, "AccentPrimaryBrush");
            BtnShapesGroup.SetResourceReference(Button.BorderBrushProperty, "AccentBorderBrush");
            BtnShapesGroup.BorderThickness = new Thickness(1);
            BtnShapesGroup.SetResourceReference(Button.ForegroundProperty, "AccentForegroundBrush");
            TxtActiveShapeIcon.SetResourceReference(TextBlock.ForegroundProperty, "AccentForegroundBrush");
        }
        else
        {
            BtnShapesGroup.Background = Brushes.Transparent;
            BtnShapesGroup.BorderBrush = Brushes.Transparent;
            BtnShapesGroup.BorderThickness = new Thickness(0);
            BtnShapesGroup.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            TxtActiveShapeIcon.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        }
        TxtActiveShapeIcon.Text = _currentTool switch
        {
            ToolType.Ellipse => "◯",
            ToolType.Line => "╱",
            _ => "▭"
        };

        // Update StepMarker group button state & active icon
        bool isMarkerGroupActive = _currentTool == ToolType.StepMarker;
        if (BtnStepMarkerGroup != null)
        {
            if (isMarkerGroupActive)
            {
                BtnStepMarkerGroup.SetResourceReference(Button.BackgroundProperty, "AccentPrimaryBrush");
                BtnStepMarkerGroup.SetResourceReference(Button.BorderBrushProperty, "AccentBorderBrush");
                BtnStepMarkerGroup.BorderThickness = new Thickness(1);
                BtnStepMarkerGroup.SetResourceReference(Button.ForegroundProperty, "AccentForegroundBrush");
            }
            else
            {
                BtnStepMarkerGroup.Background = Brushes.Transparent;
                BtnStepMarkerGroup.BorderBrush = Brushes.Transparent;
                BtnStepMarkerGroup.BorderThickness = new Thickness(0);
                BtnStepMarkerGroup.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            }
        }
        if (TxtActiveMarkerIcon != null)
        {
            if (isMarkerGroupActive)
            {
                TxtActiveMarkerIcon.SetResourceReference(TextBlock.ForegroundProperty, "AccentForegroundBrush");
            }
            else
            {
                TxtActiveMarkerIcon.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            }
            TxtActiveMarkerIcon.Text = _stepMarkerStyle switch
            {
                StepMarkerStyle.Uppercase => "🔤",
                StepMarkerStyle.Lowercase => "🔡",
                StepMarkerStyle.Dots => "⚫",
                _ => "①"
            };
        }
        if (TxtMarkerSizeValue != null)
        {
            TxtMarkerSizeValue.Text = $"{(int)_stepMarkerSize} px";
        }

        // Update Blur group button state & active icon
        bool isBlurGroupActive = _currentTool == ToolType.Blur;
        if (isBlurGroupActive)
        {
            BtnBlurGroup.SetResourceReference(Button.BackgroundProperty, "AccentPrimaryBrush");
            BtnBlurGroup.SetResourceReference(Button.BorderBrushProperty, "AccentBorderBrush");
            BtnBlurGroup.BorderThickness = new Thickness(1);
            BtnBlurGroup.SetResourceReference(Button.ForegroundProperty, "AccentForegroundBrush");
            TxtActiveBlurIcon.SetResourceReference(TextBlock.ForegroundProperty, "AccentForegroundBrush");
        }
        else
        {
            BtnBlurGroup.Background = Brushes.Transparent;
            BtnBlurGroup.BorderBrush = Brushes.Transparent;
            BtnBlurGroup.BorderThickness = new Thickness(0);
            BtnBlurGroup.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            TxtActiveBlurIcon.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        }
        TxtActiveBlurIcon.Text = _currentObfuscationStyle == ObfuscationStyle.Blur ? "💧" : "▦";
        if (TxtBlurIntensityValue != null)
        {
            TxtBlurIntensityValue.Text = $"{_currentObfuscationIntensity} px";
        }
        if (SliderBlurIntensity != null && Math.Abs(SliderBlurIntensity.Value - _currentObfuscationIntensity) > 0.5)
        {
            SliderBlurIntensity.Value = _currentObfuscationIntensity;
        }
    }

    private void UpdateColorButtonStates()
    {
        var brush = new SolidColorBrush(_currentColor);
        brush.Freeze();
        EllipseActiveColor.Fill = brush;

        if (EllipseColorPickerPreview != null)
        {
            EllipseColorPickerPreview.Fill = brush;
        }

        if (TxtColorPickerHex != null && !_isUpdatingColorPickerText)
        {
            _isUpdatingColorPickerText = true;
            try
            {
                TxtColorPickerHex.Text = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
            }
            finally
            {
                _isUpdatingColorPickerText = false;
            }
        }

        if (InlineTextInput != null && InlineTextInput.Visibility == Visibility.Visible)
        {
            InlineTextInput.Foreground = brush;
        }
    }

    #endregion

    #region Actions: Copy, Save, Editor, OCR, Gemini

    private Bitmap? GetFinalCroppedImage()
    {
        if (_fullBitmap == null || _selectionRect.IsEmpty || _selectionRect.Width < 2 || _selectionRect.Height < 2)
        {
            return null;
        }

        var cropRect = new Rectangle((int)_selectionRect.X, (int)_selectionRect.Y, (int)_selectionRect.Width, (int)_selectionRect.Height);
        using var rawCropped = ScreenCaptureService.CropBitmap(_fullBitmap, cropRect);

        return ExportService.RenderAnnotatedBitmap(rawCropped, _annotations);
    }

    private async void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        using var finalBmp = GetFinalCroppedImage();
        if (finalBmp == null) return;

        ExportService.CopyToClipboard(finalBmp);
        SoundService.PlayCaptureSound();

        string thumb = HistoryService.GenerateThumbnailBase64(finalBmp);
        await HistoryService.Instance.AddCaptureAsync(new HistoryItem
        {
            Timestamp = DateTime.Now,
            FilePath = string.Empty,
            FileName = "Área de Transferência",
            Width = finalBmp.Width,
            Height = finalBmp.Height,
            Format = "CLIPBOARD",
            ThumbnailBase64 = thumb
        });

        CloseAndCleanup();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        using var finalBmp = GetFinalCroppedImage();
        if (finalBmp == null) return;

        var settings = SettingsService.Instance.Current;
        var sfd = new SaveFileDialog
        {
            Filter = "Imagem PNG (*.png)|*.png|Imagem JPEG (*.jpg)|*.jpg",
            FileName = ExportService.GenerateFileName(settings.FilenamePattern, settings.DefaultFormat),
            InitialDirectory = settings.DefaultSaveDirectory,
            Title = "Salvar Captura de Tela"
        };

        if (sfd.ShowDialog() == true)
        {
            var fmt = sfd.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormatType.Jpg : ImageFormatType.Png;
            string savedPath = ExportService.SaveImageToFile(finalBmp, sfd.FileName, fmt, settings.JpgQuality);
            SoundService.PlayCaptureSound();

            string thumb = HistoryService.GenerateThumbnailBase64(finalBmp);
            await HistoryService.Instance.AddCaptureAsync(new HistoryItem
            {
                Timestamp = DateTime.Now,
                FilePath = savedPath,
                FileName = System.IO.Path.GetFileName(savedPath),
                Width = finalBmp.Width,
                Height = finalBmp.Height,
                Format = fmt.ToString().ToUpperInvariant(),
                ThumbnailBase64 = thumb
            });

            CloseAndCleanup();
        }
    }

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var finalBmp = GetFinalCroppedImage();
            if (finalBmp == null) return;

            var pinnedWin = new PinnedCaptureWindow(finalBmp);
            pinnedWin.Show();

            SoundService.PlayCaptureSound();
            CloseAndCleanup();
        }
        catch (Exception ex)
        {
            Logger.LogException("CaptureOverlay", "BtnPin_Click", ex);
        }
    }

    private void BtnEditor_Click(object sender, RoutedEventArgs e)
    {
        var finalBmp = GetFinalCroppedImage();
        if (finalBmp == null) return;

        CloseAndCleanup();

        var editor = new FullEditorWindow();
        editor.LoadBitmap(finalBmp);
        editor.Show();
        editor.Activate();
    }

    private void BtnOcr_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var finalBmp = GetFinalCroppedImage();
            if (finalBmp == null) return;

            CloseAndCleanup();

            var aiWindow = new AiResultWindow();
            aiWindow.Show();
            aiWindow.Activate();

            _ = aiWindow.ExecuteOcrAsync(finalBmp);
        }
        catch (Exception ex)
        {
            Logger.LogException("CaptureOverlay", "BtnOcr_Click", ex);
        }
    }

    private void BtnAskAi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var finalBmp = GetFinalCroppedImage();
            if (finalBmp == null) return;

            CloseAndCleanup();

            var aiWindow = new AiResultWindow();
            aiWindow.Show();
            aiWindow.Activate();
            aiWindow.StartChatMode(finalBmp);
        }
        catch (Exception ex)
        {
            Logger.LogException("CaptureOverlay", "BtnAskAi_Click", ex);
        }
    }

    private void BtnAiMenu_Click(object sender, RoutedEventArgs e)
    {
        var contextMenu = new ContextMenu();

        AddAiMenuItem(contextMenu, "✦ Analisar Imagem Completa", AiActionType.Analyze);
        AddAiMenuItem(contextMenu, "✦ Extrair Texto Estruturado", AiActionType.ExtractText);
        AddAiMenuItem(contextMenu, "✦ Traduzir para Português", AiActionType.Translate);
        AddAiMenuItem(contextMenu, "✦ Resumir Conteúdo", AiActionType.Summarize);
        AddAiMenuItem(contextMenu, "✦ Diagnosticar e Explicar Erro", AiActionType.ExplainError);
        AddAiMenuItem(contextMenu, "✦ Perguntar à IA...", AiActionType.AskQuestion);
        AddAiMenuItem(contextMenu, "✦ Identificar Elementos / Interface", AiActionType.IdentifyElements);
        AddAiMenuItem(contextMenu, "✦ Pesquisar na Web com Gemini", AiActionType.SearchWeb);

        contextMenu.PlacementTarget = BtnAiMenu;
        contextMenu.IsOpen = true;
    }

    private void AddAiMenuItem(ContextMenu menu, string header, AiActionType action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (s, e) =>
        {
            try
            {
                var finalBmp = GetFinalCroppedImage();
                if (finalBmp == null) return;

                CloseAndCleanup();

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
                Logger.LogException("CaptureOverlay", "AddAiMenuItem_Click", ex);
            }
        };
        menu.Items.Add(item);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        CloseAndCleanup();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var sm = ShortcutManager.Instance;
        var settings = SettingsService.Instance.Current;

        if (sm.IsActionTriggered(ShortcutManager.ActionCancel, e, settings))
        {
            if (InlineTextInput.Visibility == Visibility.Visible)
            {
                InlineTextInput.Visibility = Visibility.Collapsed;
            }
            else
            {
                CloseAndCleanup();
            }
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionCopy, e, settings))
        {
            if (!_selectionRect.IsEmpty && _selectionRect.Width >= 2 && _selectionRect.Height >= 2)
            {
                BtnCopy_Click(this, new RoutedEventArgs());
            }
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionSave, e, settings))
        {
            if (!_selectionRect.IsEmpty && _selectionRect.Width >= 2 && _selectionRect.Height >= 2)
            {
                BtnSave_Click(this, new RoutedEventArgs());
            }
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionPin, e, settings))
        {
            if (!_selectionRect.IsEmpty && _selectionRect.Width >= 2 && _selectionRect.Height >= 2)
            {
                BtnPin_Click(this, new RoutedEventArgs());
            }
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionEditor, e, settings))
        {
            if (!_selectionRect.IsEmpty && _selectionRect.Width >= 2 && _selectionRect.Height >= 2)
            {
                BtnEditor_Click(this, new RoutedEventArgs());
            }
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionAi, e, settings))
        {
            if (!_selectionRect.IsEmpty && _selectionRect.Width >= 2 && _selectionRect.Height >= 2)
            {
                BtnAskAi_Click(this, new RoutedEventArgs());
            }
            e.Handled = true;
        }
        else if (sm.IsActionTriggered(ShortcutManager.ActionOcr, e, settings))
        {
            if (!_selectionRect.IsEmpty && _selectionRect.Width >= 2 && _selectionRect.Height >= 2)
            {
                BtnOcr_Click(this, new RoutedEventArgs());
            }
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
    }

    private void CloseAndCleanup()
    {
        try
        {
            CloseAllFlyouts();
            ThemeService.Instance.ThemeChanged -= OnThemeChanged;
            Hide();
            _fullBitmap?.Dispose();
            _fullBitmap = null;
            _cachedScreenPixels = null;
            BackgroundImage.Source = null;
        }
        catch (Exception ex)
        {
            Logger.LogException("CaptureOverlay", "CloseAndCleanup", ex);
        }
        finally
        {
            try { Close(); } catch { }
        }
    }

    #endregion
}
