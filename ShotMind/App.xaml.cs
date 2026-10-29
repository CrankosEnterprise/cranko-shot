using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows;
using ShotMind.Core.Services;
using ShotMind.UI;
using ShotMind.UI.Views;

namespace ShotMind;

public partial class App : Application
{
    private const string AppMutexName = "Local\\ShotMind_Unique_Instance_Mutex";
    private const string ActivateEventName = "Local\\ShotMind_Activate_Event";
    private Mutex? _appMutex;
    private EventWaitHandle? _activateEvent;
    private CancellationTokenSource? _ipcCts;
    private TrayIconManager? _trayManager;
    private CaptureOverlayWindow? _activeOverlay;
    private SettingsWindow? _activeSettingsWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 0. Global Unhandled Exception Protection
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        try
        {
            if (!string.IsNullOrEmpty(AppContext.BaseDirectory))
            {
                System.IO.Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            }
        }
        catch { }

        // Command-line automatic Windows startup detection
        bool isAutoStartup = e.Args.Any(a =>
            a.Equals("--background", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-background", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/background", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--autostart", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--startup", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-silent", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-s", StringComparison.OrdinalIgnoreCase));

        try
        {
            // 1. Single Instance Check (protected against UAC / permission differences on Windows 10)
            bool createdNew = false;
            try
            {
                _appMutex = new Mutex(true, AppMutexName, out createdNew);
            }
            catch (UnauthorizedAccessException)
            {
                createdNew = false;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                createdNew = false;
            }

            if (!createdNew)
            {
                // If launched manually by the user while already running, signal the active instance to open the window
                if (!isAutoStartup)
                {
                    bool signaled = false;
                    try
                    {
                        using var evt = EventWaitHandle.OpenExisting(ActivateEventName);
                        signaled = evt.Set();
                    }
                    catch
                    {
                        signaled = false;
                    }

                    if (!signaled)
                    {
                        MessageBox.Show(
                            "O CrankoShot já está em execução em segundo plano.\nVerifique o ícone na bandeja do sistema (perto do relógio).",
                            "CrankoShot",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                Shutdown();
                return;
            }

            Logger.Info("Iniciando CrankoShot...");

            // 2. Initialize Services
            SettingsService.Instance.Load();
            if (SettingsService.Instance.Current.RunOnStartup || StartupService.IsRunOnStartupEnabled())
            {
                StartupService.SetRunOnStartup(true);
            }
            ThemeService.Instance.Initialize();

            // 3. Tray Icon (initialized before async DB so tray & UI are immediately responsive)
            _trayManager = new TrayIconManager();
            _trayManager.CaptureRequested += TriggerCapture;
            _trayManager.EditorRequested += OpenEditor;
            _trayManager.HistoryRequested += OpenHistory;
            _trayManager.SettingsRequested += OpenSettings;
            _trayManager.ExitRequested += ExitApp;
            _trayManager.Initialize();

            // 4. Global Hotkey
            if (SettingsService.Instance.Current.OverrideWindowsSnippingTool && !WindowsSnippingService.IsSnippingOverrideActive())
            {
                _ = Task.Run(() => WindowsSnippingService.SetSnippingOverride(true));
            }
            HotkeyService.Instance.HotkeyPressed += TriggerCapture;
            HotkeyService.Instance.HotkeyRegistrationFailed += (msg) =>
            {
                _trayManager.ShowNotification("Aviso de Atalho", msg, System.Windows.Forms.ToolTipIcon.Warning);
            };
            HotkeyService.Instance.Initialize();

            // 5. Start inter-process activation listener for subsequent manual launches
            StartActivationListener();

            // 6. Open main window on explicit manual or post-install launch; stay silent on Windows auto-startup (--background)
            if (!isAutoStartup)
            {
                OpenSettings();

                string hotkeyDesc = HotkeyService.FormatHotkeyDisplay(SettingsService.Instance.Current.HotkeyModifiers, SettingsService.Instance.Current.HotkeyKey);
                _trayManager.ShowNotification(
                    "CrankoShot Ativo",
                    $"Pressione {hotkeyDesc} para capturar ou use o ícone na bandeja.",
                    System.Windows.Forms.ToolTipIcon.Info);
            }

            // 7. Initialize SQLite history database
            await HistoryService.Instance.InitializeAsync();

            Logger.Info($"CrankoShot iniciado com sucesso (autoStartup={isAutoStartup}).");
        }
        catch (Exception ex)
        {
            Logger.LogException("App", "OnStartup", ex);
            MessageBox.Show($"Erro ao iniciar o CrankoShot: {ex.Message}", "Erro Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void StartActivationListener()
    {
        try
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            _ipcCts = new CancellationTokenSource();
            var token = _ipcCts.Token;

            Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        int idx = WaitHandle.WaitAny(new WaitHandle[] { _activateEvent, token.WaitHandle });
                        if (idx == 0 && !token.IsCancellationRequested)
                        {
                            OpenSettings();
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            }, token);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Não foi possível iniciar ouvinte de ativação: {ex.Message}");
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogException("WPF.Dispatcher", "DispatcherUnhandledException", e.Exception);
        e.Handled = true; // Impede que uma exceção na interface encerre a aplicação
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.LogException("AppDomain", "CurrentDomain_UnhandledException", ex);
        }
        else
        {
            Logger.Error($"Exceção não tratada no AppDomain: {e.ExceptionObject}");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Logger.LogException("TaskScheduler", "UnobservedTaskException", e.Exception);
        e.SetObserved(); // Impede o encerramento do processo por exceções em tarefas assíncronas
    }

    private void RecheckHotkey()
    {
        var (success, message) = HotkeyService.Instance.ReRegisterHotkey();
        var icon = success ? System.Windows.Forms.ToolTipIcon.Info : System.Windows.Forms.ToolTipIcon.Warning;
        _trayManager?.ShowNotification(success ? "Atalho Atualizado" : "Aviso de Atalho", message, icon);
    }

    private void TriggerCapture()
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                // Se a janela de captura já estiver aberta e visível, foca nela
                if (_activeOverlay != null && _activeOverlay.IsVisible)
                {
                    _activeOverlay.Activate();
                    _activeOverlay.Focus();
                    return;
                }

                _activeOverlay = null;

                var bounds = ScreenCaptureService.GetVirtualScreenBounds();
                var desktopBmp = ScreenCaptureService.CaptureVirtualScreen();

                var overlay = new CaptureOverlayWindow();
                _activeOverlay = overlay;
                overlay.Closed += (s, e) =>
                {
                    if (_activeOverlay == overlay)
                    {
                        _activeOverlay = null;
                    }
                };
                overlay.Unloaded += (s, e) =>
                {
                    if (_activeOverlay == overlay)
                    {
                        _activeOverlay = null;
                    }
                };

                overlay.StartCapture(desktopBmp, bounds);
            }
            catch (Exception ex)
            {
                _activeOverlay = null;
                Logger.LogException("ScreenCapture", "TriggerCapture", ex);
            }
        });
    }

    private void OpenEditor()
    {
        Dispatcher.Invoke(() =>
        {
            var editor = new FullEditorWindow();
            // Start with a blank canvas or prompt
            using var blank = new Bitmap(1280, 720);
            using (var g = Graphics.FromImage(blank))
            {
                g.Clear(System.Drawing.Color.FromArgb(30, 30, 35));
            }
            editor.LoadBitmap(blank);
            editor.Show();
            editor.Activate();
        });
    }

    private void OpenHistory()
    {
        Dispatcher.Invoke(() =>
        {
            var history = new HistoryWindow();
            history.Show();
            history.Activate();
        });
    }

    private void OpenSettings()
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                if (_activeSettingsWindow != null && _activeSettingsWindow.IsLoaded)
                {
                    if (_activeSettingsWindow.WindowState == WindowState.Minimized)
                    {
                        _activeSettingsWindow.WindowState = WindowState.Normal;
                    }
                    _activeSettingsWindow.Show();
                    _activeSettingsWindow.Activate();
                    _activeSettingsWindow.Focus();
                    return;
                }

                var settings = new SettingsWindow();
                _activeSettingsWindow = settings;
                settings.Closed += (s, e) =>
                {
                    if (_activeSettingsWindow == settings)
                    {
                        _activeSettingsWindow = null;
                    }
                };
                settings.Show();
                settings.Activate();
                settings.Focus();
            }
            catch (Exception ex)
            {
                Logger.LogException("App", "OpenSettings", ex);
            }
        });
    }

    private void ExitApp()
    {
        Logger.Info("Encerrando CrankoShot.");
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _ipcCts?.Cancel(); } catch { }
        try { _activateEvent?.Dispose(); } catch { }
        try { _ipcCts?.Dispose(); } catch { }

        HotkeyService.Instance.Dispose();
        _trayManager?.Dispose();

        if (_appMutex != null)
        {
            try { _appMutex.ReleaseMutex(); } catch { }
            _appMutex.Dispose();
            _appMutex = null;
        }

        base.OnExit(e);
    }
}
