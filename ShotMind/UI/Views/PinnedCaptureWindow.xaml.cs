using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ShotMind.Core;
using ShotMind.Core.Models;
using ShotMind.Core.Services;

namespace ShotMind.UI.Views;

public partial class PinnedCaptureWindow : Window
{
    private static readonly List<PinnedCaptureWindow> _activePins = new();

    private Bitmap? _pinnedBitmap;
    private readonly double _aspectRatio = 1.0;
    private bool _isResizing = false;

    public PinnedCaptureWindow(Bitmap image)
    {
        InitializeComponent();

        _pinnedBitmap = new Bitmap(image);
        _aspectRatio = (double)_pinnedBitmap.Width / Math.Max(1, _pinnedBitmap.Height);

        TxtDimensions.Text = $"{_pinnedBitmap.Width} × {_pinnedBitmap.Height} px";

        // Converter bitmap para BitmapSource do WPF
        using (var ms = new MemoryStream())
        {
            _pinnedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            ImgPinnedContent.Source = bi;
        }

        CalculateInitialPosition();

        _activePins.Add(this);
        Closed += (s, e) =>
        {
            _activePins.Remove(this);
            DisposeResources();
        };

        SizeChanged += PinnedCaptureWindow_SizeChanged;
    }

    private void CalculateInitialPosition()
    {
        // Limita o tamanho inicial preservando proporção
        double maxInitialW = 460;
        double maxInitialH = 340;

        double targetW = _pinnedBitmap!.Width;
        double targetH = _pinnedBitmap!.Height;

        if (targetW > maxInitialW || targetH > maxInitialH)
        {
            double scale = Math.Min(maxInitialW / targetW, maxInitialH / targetH);
            targetW *= scale;
            targetH *= scale;
        }

        // Adiciona margens da moldura
        this.Width = Math.Max(180, targetW + 16);
        this.Height = Math.Max(130, targetH + 16);

        // Posiciona no canto superior direito do monitor principal ou ativo
        var workArea = SystemParameters.WorkArea;
        int cascadeIndex = _activePins.Count % 8;
        double cascadeOffset = cascadeIndex * 28.0;

        double left = workArea.Right - this.Width - 24 - cascadeOffset;
        double top = workArea.Top + 24 + cascadeOffset;

        if (left < workArea.Left) left = workArea.Left + 16;
        if (top + this.Height > workArea.Bottom) top = workArea.Bottom - this.Height - 16;

        this.Left = left;
        this.Top = top;
    }

    private void PinnedCaptureWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isResizing || !e.WidthChanged && !e.HeightChanged) return;

        // Mantém a proporção do conteúdo sem distorcer a imagem
        _isResizing = true;
        try
        {
            if (e.WidthChanged)
            {
                double desiredH = (this.Width - 16) / _aspectRatio + 16;
                this.Height = Math.Max(MinHeight, desiredH);
            }
            else if (e.HeightChanged)
            {
                double desiredW = (this.Height - 16) * _aspectRatio + 16;
                this.Width = Math.Max(MinWidth, desiredW);
            }
        }
        finally
        {
            _isResizing = false;
        }
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150));
        FloatingToolbar.BeginAnimation(OpacityProperty, anim);
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        var anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
        FloatingToolbar.BeginAnimation(OpacityProperty, anim);
    }

    private void FloatingToolbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            this.DragMove();
        }
    }

    private void ImgPinnedContent_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            this.DragMove();
        }
    }

    private void BtnToggleTopmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        if (Topmost)
        {
            TxtPinIcon.Text = "📌";
            OuterBorder.SetResourceReference(Border.BorderBrushProperty, "AccentPrimaryBrush");
            BtnToggleTopmost.ToolTip = "Sempre visível (Ativo) — Clique para desafixar";
        }
        else
        {
            TxtPinIcon.Text = "📍";
            OuterBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
            BtnToggleTopmost.ToolTip = "Fixado como janela comum — Clique para deixar sempre visível";
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_pinnedBitmap == null) return;
        ExportService.CopyToClipboard(_pinnedBitmap);
        SoundService.PlayCaptureSound();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_pinnedBitmap == null) return;

        var settings = SettingsService.Instance.Current;
        var sfd = new SaveFileDialog
        {
            Filter = "Imagem PNG (*.png)|*.png|Imagem JPEG (*.jpg)|*.jpg",
            FileName = ExportService.GenerateFileName(settings.FilenamePattern, settings.DefaultFormat),
            InitialDirectory = settings.DefaultSaveDirectory,
            Title = "Salvar Captura Fixada"
        };

        if (sfd.ShowDialog() == true)
        {
            var fmt = sfd.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormatType.Jpg : ImageFormatType.Png;
            ExportService.SaveImageToFile(_pinnedBitmap, sfd.FileName, fmt, settings.JpgQuality);
            SoundService.PlayCaptureSound();
        }
    }

    private void BtnEditor_Click(object sender, RoutedEventArgs e)
    {
        if (_pinnedBitmap == null) return;

        var editor = new FullEditorWindow();
        editor.LoadBitmap(new Bitmap(_pinnedBitmap));
        editor.Show();
        editor.Activate();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ShortcutManager.Instance.IsActionTriggered(e, "Cancel") || e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (ShortcutManager.Instance.IsActionTriggered(e, "Copy"))
        {
            BtnCopy_Click(sender, e);
            e.Handled = true;
        }
        else if (ShortcutManager.Instance.IsActionTriggered(e, "Save"))
        {
            BtnSave_Click(sender, e);
            e.Handled = true;
        }
        else if (ShortcutManager.Instance.IsActionTriggered(e, "Editor"))
        {
            BtnEditor_Click(sender, e);
            e.Handled = true;
        }
    }

    private void DisposeResources()
    {
        try
        {
            _pinnedBitmap?.Dispose();
            _pinnedBitmap = null;
            ImgPinnedContent.Source = null;
        }
        catch (Exception ex)
        {
            Logger.LogException("PinnedCaptureWindow", "DisposeResources", ex);
        }
    }
}
