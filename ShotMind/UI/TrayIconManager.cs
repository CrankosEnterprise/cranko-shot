using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShotMind.Core.Services;

namespace ShotMind.UI;

public class TrayIconManager : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private Icon? _appIcon;

    public event Action? CaptureRequested;
    public event Action? EditorRequested;
    public event Action? HistoryRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public void Initialize()
    {
        _appIcon = CreateAppIcon();

        var contextMenu = new ContextMenuStrip();
        
        var captureItem = new ToolStripMenuItem("Capturar Tela", null, (s, e) => CaptureRequested?.Invoke());
        captureItem.Font = new Font(captureItem.Font, System.Drawing.FontStyle.Bold);

        var editorItem = new ToolStripMenuItem("Editor de Imagens...", null, (s, e) => EditorRequested?.Invoke());
        var historyItem = new ToolStripMenuItem("Histórico de Capturas...", null, (s, e) => HistoryRequested?.Invoke());
        var settingsItem = new ToolStripMenuItem("Configurações...", null, (s, e) => SettingsRequested?.Invoke());
        var exitItem = new ToolStripMenuItem("Sair do CrankoShot", null, (s, e) => ExitRequested?.Invoke());

        contextMenu.Items.Add(captureItem);
        contextMenu.Items.Add(editorItem);
        contextMenu.Items.Add(historyItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "CrankoShot — Captura & Inteligência Visual",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => CaptureRequested?.Invoke();
        _notifyIcon.BalloonTipClicked += (s, e) => SettingsRequested?.Invoke();
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        try
        {
            if (SettingsService.Instance.Current.ShowNotifications && _notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, icon);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Não foi possível exibir notificação na bandeja: {ex.Message}");
        }
    }

    private static Icon CreateAppIcon()
    {
        // Programmatically generate a modern 32x32 icon with purple gradient & crosshair
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            // Purple rounded rectangle background
            using var brush = new LinearGradientBrush(
                new System.Drawing.Point(0, 0),
                new System.Drawing.Point(32, 32),
                System.Drawing.Color.FromArgb(139, 92, 246),
                System.Drawing.Color.FromArgb(109, 40, 217));

            g.FillRoundedRectangle(brush, new Rectangle(1, 1, 30, 30), 7);

            // Inner dark circle for aperture / camera
            using var darkBrush = new SolidBrush(System.Drawing.Color.FromArgb(24, 24, 27));
            g.FillEllipse(darkBrush, 6, 6, 20, 20);

            // Purple center dot
            using var purpleDot = new SolidBrush(System.Drawing.Color.FromArgb(167, 139, 250));
            g.FillEllipse(purpleDot, 12, 12, 8, 8);
        }

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        if (_appIcon != null)
        {
            _appIcon.Dispose();
            _appIcon = null;
        }
    }
}

public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, System.Drawing.Brush brush, Rectangle bounds, int cornerRadius)
    {
        using var path = CreateRoundedRectanglePath(bounds, cornerRadius);
        g.FillPath(brush, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        var arc = new Rectangle(bounds.Location, new System.Drawing.Size(diameter, diameter));

        // Top left
        path.AddArc(arc, 180, 90);
        // Top right
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        // Bottom right
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        // Bottom left
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }
}
