using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShotMind.Core;
using ShotMind.Core.Models;
using ShotMind.Core.Services;

namespace ShotMind.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly List<GeminiModelInfo> _availableModels = new();
    private readonly List<OpenRouterModelInfo> _availableOpenRouterModels = new();
    private AiProviderType _selectedAiProvider = AiProviderType.Gemini;
    private bool _isLoadingAiUI = false;
    private bool _isRecordingHotkey = false;
    private string? _recordingActionId = null;
    private bool _isLoadingCrosshairUI = false;
    private CrosshairStyle _selectedCrosshairStyle = CrosshairStyle.Simple;

    private WriteableBitmap? _previewDarkBmp;
    private WriteableBitmap? _previewLightBmp;
    private readonly int[] _previewDarkPixels = new int[CrosshairService.GridSize * CrosshairService.GridSize];
    private readonly int[] _previewLightPixels = new int[CrosshairService.GridSize * CrosshairService.GridSize];
    private readonly int[] _simulatedDarkScreen = new int[CrosshairService.GridSize * CrosshairService.GridSize];
    private readonly int[] _simulatedLightScreen = new int[CrosshairService.GridSize * CrosshairService.GridSize];
    private readonly Int32Rect _previewDirtyRect = new(0, 0, CrosshairService.GridSize, CrosshairService.GridSize);

    public SettingsWindow()
    {
        _isLoadingAiUI = true;
        _isLoadingCrosshairUI = true;
        try
        {
            InitializeComponent();
        }
        finally
        {
            _isLoadingAiUI = false;
            _isLoadingCrosshairUI = false;
        }
        InitializeCrosshairPreviewBitmaps();
    }

    private void InitializeCrosshairPreviewBitmaps()
    {
        _previewDarkBmp = new WriteableBitmap(CrosshairService.GridSize, CrosshairService.GridSize, 96, 96, PixelFormats.Bgra32, null);
        _previewLightBmp = new WriteableBitmap(CrosshairService.GridSize, CrosshairService.GridSize, 96, 96, PixelFormats.Bgra32, null);

        if (ImgPreviewCrosshairDark != null) ImgPreviewCrosshairDark.Source = _previewDarkBmp;
        if (ImgPreviewCrosshairLight != null) ImgPreviewCrosshairLight.Source = _previewLightBmp;

        int darkPixel = unchecked((int)0xFF101017);
        int lightPixel = unchecked((int)0xFFF8FAFC);
        Array.Fill(_simulatedDarkScreen, darkPixel);
        Array.Fill(_simulatedLightScreen, lightPixel);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettingsIntoUI();
        LoadOcrInfo();
    }

    private void NavSidebar_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (NavSidebar == null || PaneGeral == null) return;

        int index = NavSidebar.SelectedIndex;

        PaneGeral.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        PaneCaptura.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        PaneCursor.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PaneEdicao.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        PaneSalvamento.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
        PaneIA.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
        PaneOCR.Visibility = index == 6 ? Visibility.Visible : Visibility.Collapsed;

        switch (index)
        {
            case 0:
                TxtCategoryTitle.Text = "Configurações Gerais";
                TxtCategorySubtitle.Text = "Inicialização, notificações e comportamento do sistema.";
                break;
            case 1:
                TxtCategoryTitle.Text = "Captura & Atalhos";
                TxtCategorySubtitle.Text = "Atalhos globais de teclado e tecla Print Screen.";
                break;
            case 2:
                TxtCategoryTitle.Text = "Aparência & Mira";
                TxtCategorySubtitle.Text = "Tema glassmorphism, formatos da mira, inversão localizada e prévia em tempo real.";
                break;
            case 3:
                TxtCategoryTitle.Text = "Edição & Censura";
                TxtCategorySubtitle.Text = "Padrões de desfoque e pixelização.";
                break;
            case 4:
                TxtCategoryTitle.Text = "Salvamento & Nomes";
                TxtCategorySubtitle.Text = "Destino, formatos e padrão de nomes de arquivos.";
                break;
            case 5:
                TxtCategoryTitle.Text = "Inteligência Artificial";
                TxtCategorySubtitle.Text = "Provedores Google Gemini e OpenRouter, chaves de API e seleção de modelos.";
                break;
            case 6:
                TxtCategoryTitle.Text = "OCR Local";
                TxtCategorySubtitle.Text = "Reconhecimento de texto nativo do Windows.";
                break;
        }
    }

    private void LoadSettingsIntoUI()
    {
        var s = SettingsService.Instance.Current;

        // Geral
        ChkRunOnStartup.IsChecked = StartupService.IsRunOnStartupEnabled();
        ChkMinimizeToTray.IsChecked = s.MinimizeToTray;
        ChkPlaySound.IsChecked = s.PlaySoundOnCapture;
        ChkNotifications.IsChecked = s.ShowNotifications;

        // Atalhos
        PopulateHotkeyKeys();
        TxtCurrentHotkeyBadge.Text = HotkeyService.FormatHotkeyDisplay(s.HotkeyModifiers, s.HotkeyKey);
        SelectModifierInComboBox(s.HotkeyModifiers);
        SelectKeyInComboBox(s.HotkeyKey);
        TxtHotkeyStatusText.Text = HotkeyService.Instance.IsRegistered
            ? "✓ Atalho ativo e pronto para uso"
            : "⚠️ Atalho não registrado no momento";

        // Atalhos de Ações
        PopulateActionShortcutsList();

        // Temas de Cores
        UpdateThemeCardsSelection();

        // Windows Snipping override
        ChkOverrideWindowsSnipping.IsChecked = s.OverrideWindowsSnippingTool || WindowsSnippingService.IsSnippingOverrideActive();

        // Captura e Salvamento
        SetComboBoxItem(CmbDefaultAction, (int)s.DefaultAction);
        TxtSaveDirectory.Text = s.DefaultSaveDirectory;
        SetComboBoxItem(CmbImageFormat, (int)s.DefaultFormat);
        TxtJpgQuality.Text = s.JpgQuality.ToString();
        TxtFilenamePattern.Text = string.IsNullOrWhiteSpace(s.FilenamePattern) ? "ShotMind_{yyyy-MM-dd_HH-mm-ss}" : s.FilenamePattern;
        TxtSequenceCounter.Text = s.SequenceCounter.ToString();
        SetComboBoxItem(CmbSequenceDigits, Math.Clamp(s.SequenceDigits - 1, 0, 4));
        UpdateFilenamePreview();

        // Censura / Ocultação
        SetComboBoxItem(CmbObfuscationStyle, (int)s.DefaultObfuscationStyle);
        TxtObfuscationIntensity.Text = s.ObfuscationIntensity.ToString();

        // Cursor & Seleção (Mira)
        _isLoadingCrosshairUI = true;
        try
        {
            _selectedCrosshairStyle = s.CrosshairStyle;
            ChkAutoContrastCursor.IsChecked = s.AutoContrastCursor;
            ChkCrosshairOutline.IsChecked = s.CrosshairOutline;
            ChkCrosshairShowDot.IsChecked = s.CrosshairShowDot || s.CrosshairStyle is CrosshairStyle.CenterDot or CrosshairStyle.LinesAndDot;
            SliderCrosshairThickness.Value = Math.Clamp(s.CrosshairThickness, 1, 6);
            SliderCrosshairLength.Value = Math.Clamp(s.CrosshairLength, 4, 28);
            SliderCrosshairGap.Value = Math.Clamp(s.CrosshairGap, 1, 14);
            SliderCrosshairDotSize.Value = Math.Clamp(s.CrosshairDotSize, 1, 10);

            TxtCursorColorHex.Text = string.IsNullOrEmpty(s.CursorColor) ? "#8B5CF6" : s.CursorColor;
            UpdateCursorColorPreview(TxtCursorColorHex.Text);
            SetComboBoxItem(CmbCrosshairStyle, (int)_selectedCrosshairStyle);
            SetComboBoxItem(CmbCursorSize, s.CursorSize switch
            {
                14 => 0,
                24 => 2,
                _ => 1 // 18 padrão
            });
        }
        finally
        {
            _isLoadingCrosshairUI = false;
        }
        UpdateCrosshairStyleButtons();
        UpdateCursorControlsState();
        RefreshCrosshairLivePreview();

        // IA
        _isLoadingAiUI = true;
        try
        {
            _selectedAiProvider = s.AiProvider;
            ChkOpenRouterFreeOnly.IsChecked = s.OpenRouterFreeModelsOnly;

            if (SettingsService.Instance.HasApiKey())
            {
                TxtApiKeyStatus.Text = $"Chave configurada: {SettingsService.Instance.GetMaskedApiKey()}";
                TxtApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            }
            else
            {
                TxtApiKeyStatus.Text = "Nenhuma chave configurada para o Google Gemini.";
                TxtApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush");
            }

            if (SettingsService.Instance.HasOpenRouterApiKey())
            {
                TxtOpenRouterApiKeyStatus.Text = $"Chave configurada: {SettingsService.Instance.GetMaskedOpenRouterApiKey()}";
                TxtOpenRouterApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            }
            else
            {
                TxtOpenRouterApiKeyStatus.Text = "Nenhuma chave configurada para o OpenRouter.";
                TxtOpenRouterApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush");
            }

            LoadInitialModels(s.GeminiModel);
            LoadInitialOpenRouterModels(s.OpenRouterModel, s.OpenRouterFreeModelsOnly);
            ChkAutoAnalyze.IsChecked = s.AutoAnalyzeEnabled;
            UpdateAiProviderSelectionUI();
        }
        finally
        {
            _isLoadingAiUI = false;
        }
    }

    #region Atalhos & Teclado Handlers

    private void PopulateHotkeyKeys()
    {
        CmbHotkeyKey.Items.Clear();
        CmbHotkeyKey.Items.Add("Print Screen");
        for (int i = 1; i <= 12; i++)
        {
            CmbHotkeyKey.Items.Add($"F{i}");
        }
        for (char c = 'A'; c <= 'Z'; c++)
        {
            CmbHotkeyKey.Items.Add(c.ToString());
        }
        for (char c = '0'; c <= '9'; c++)
        {
            CmbHotkeyKey.Items.Add(c.ToString());
        }
        CmbHotkeyKey.Items.Add("Insert");
        CmbHotkeyKey.Items.Add("Pause");
        CmbHotkeyKey.Items.Add("Scroll");
    }

    private void SelectModifierInComboBox(string? modifier)
    {
        if (string.IsNullOrWhiteSpace(modifier) || modifier.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            CmbHotkeyModifier.SelectedIndex = 0;
            return;
        }

        for (int i = 0; i < CmbHotkeyModifier.Items.Count; i++)
        {
            if (CmbHotkeyModifier.Items[i] is System.Windows.Controls.ComboBoxItem cbi &&
                string.Equals(cbi.Content?.ToString(), modifier, StringComparison.OrdinalIgnoreCase))
            {
                CmbHotkeyModifier.SelectedIndex = i;
                return;
            }
        }
        CmbHotkeyModifier.SelectedIndex = 0;
    }

    private void SelectKeyInComboBox(string? keyName)
    {
        string formatted = HotkeyService.FormatKeyName(keyName);
        for (int i = 0; i < CmbHotkeyKey.Items.Count; i++)
        {
            string? itemStr = CmbHotkeyKey.Items[i]?.ToString();
            if (string.Equals(itemStr, formatted, StringComparison.OrdinalIgnoreCase) ||
                (formatted.Equals("Print Screen", StringComparison.OrdinalIgnoreCase) && itemStr == "Print Screen"))
            {
                CmbHotkeyKey.SelectedIndex = i;
                return;
            }
        }
        CmbHotkeyKey.Items.Add(formatted);
        CmbHotkeyKey.SelectedItem = formatted;
    }

    private string GetSelectedModifier()
    {
        if (CmbHotkeyModifier.SelectedItem is System.Windows.Controls.ComboBoxItem cbi)
        {
            string content = cbi.Content?.ToString() ?? "Nenhum";
            return content.Equals("Nenhum", StringComparison.OrdinalIgnoreCase) ? "None" : content;
        }
        return "None";
    }

    private string GetSelectedKey()
    {
        string? selected = CmbHotkeyKey.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected) || selected.Equals("Print Screen", StringComparison.OrdinalIgnoreCase))
        {
            return "Snapshot";
        }
        return selected;
    }

    private void BtnRecordHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingHotkey)
        {
            CancelHotkeyRecording();
            return;
        }

        _isRecordingHotkey = true;
        TxtBtnRecordText.Text = "Ouvindo teclas...";
        BorderRecordingIndicator.Visibility = Visibility.Visible;
        BorderHotkeyFeedback.Visibility = Visibility.Collapsed;
        this.Focus();
    }

    private void CancelHotkeyRecording()
    {
        _isRecordingHotkey = false;
        TxtBtnRecordText.Text = "Alterar atalho";
        BorderRecordingIndicator.Visibility = Visibility.Collapsed;
    }

    private void PopulateActionShortcutsList()
    {
        if (StackActionShortcutsList == null) return;
        StackActionShortcutsList.Children.Clear();

        var sm = ShortcutManager.Instance;

        foreach (var action in ShortcutManager.RegisteredActions.Where(a => !a.IsGlobal))
        {
            string currentShortcut = sm.GetShortcutForAction(action.Id);

            var rowBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x1A)),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: Name & Description
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var txtTitle = new TextBlock
            {
                Text = action.DisplayName,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            var txtDesc = new TextBlock
            {
                Text = action.Description,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            infoStack.Children.Add(txtTitle);
            infoStack.Children.Add(txtDesc);
            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            // Right: Badge + Alterar + Restaurar
            var actionStack = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var badgeBorder = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("AccentSubtleBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var txtBadge = new TextBlock
            {
                Text = string.IsNullOrEmpty(currentShortcut) ? "(Nenhum)" : currentShortcut,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            badgeBorder.Child = txtBadge;
            actionStack.Children.Add(badgeBorder);

            var btnChange = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 6, 0),
                Content = "Alterar",
                Tag = action.Id
            };
            string capturedId = action.Id;
            string capturedName = action.DisplayName;
            btnChange.Click += (s, e) => StartRecordingActionShortcut(capturedId, capturedName);
            actionStack.Children.Add(btnChange);

            var btnReset = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("ToolbarIconButton"),
                Width = 28,
                Height = 28,
                ToolTip = "Restaurar atalho padrão",
                Tag = action.Id
            };
            var resetIcon = new TextBlock { Text = "↺", FontSize = 13, Foreground = Brushes.White };
            btnReset.Content = resetIcon;
            btnReset.Click += (s, e) =>
            {
                CancelActionShortcutRecording();
                sm.RestoreDefault(capturedId);
                PopulateActionShortcutsList();
                ShowActionShortcutFeedback($"Atalho da ação '{capturedName}' restaurado para o padrão ({action.DefaultShortcut}).", isError: false);
            };
            actionStack.Children.Add(btnReset);

            Grid.SetColumn(actionStack, 1);
            grid.Children.Add(actionStack);

            rowBorder.Child = grid;
            StackActionShortcutsList.Children.Add(rowBorder);
        }
    }

    private void StartRecordingActionShortcut(string actionId, string actionName)
    {
        CancelHotkeyRecording();
        _recordingActionId = actionId;
        BorderRecordingActionIndicator.Visibility = Visibility.Visible;
        TxtRecordingActionPrompt.Text = $"Pressione a combinação de teclas para '{actionName}'... (Pressione Esc para cancelar)";
        BorderActionShortcutFeedback.Visibility = Visibility.Collapsed;
        this.Focus();
    }

    private void CancelActionShortcutRecording()
    {
        _recordingActionId = null;
        if (BorderRecordingActionIndicator != null)
        {
            BorderRecordingActionIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowActionShortcutFeedback(string message, bool isError)
    {
        if (BorderActionShortcutFeedback == null || TxtActionShortcutFeedback == null) return;
        BorderActionShortcutFeedback.Visibility = Visibility.Visible;
        TxtActionShortcutFeedback.Text = message;

        if (isError)
        {
            BorderActionShortcutFeedback.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xEF, 0x44, 0x44));
            BorderActionShortcutFeedback.BorderBrush = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            TxtActionShortcutFeedback.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
        }
        else
        {
            BorderActionShortcutFeedback.Background = (System.Windows.Media.Brush)FindResource("AccentSubtleBrush");
            BorderActionShortcutFeedback.BorderBrush = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            TxtActionShortcutFeedback.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        }
    }

    private void BtnRestoreAllShortcuts_Click(object sender, RoutedEventArgs e)
    {
        CancelActionShortcutRecording();
        ShortcutManager.Instance.RestoreAllDefaults();
        PopulateActionShortcutsList();
        ShowActionShortcutFeedback("Todos os atalhos de ações foram restaurados para os padrões originais.", isError: false);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_recordingActionId != null)
        {
            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                CancelActionShortcutRecording();
                ShowActionShortcutFeedback("Alteração de atalho cancelada.", isError: false);
                return;
            }

            Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

            if (actualKey is Key.LeftCtrl or Key.RightCtrl or
                              Key.LeftAlt or Key.RightAlt or
                              Key.LeftShift or Key.RightShift or
                              Key.LWin or Key.RWin)
            {
                return;
            }

            var mods = new List<string>();
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                mods.Add("Ctrl");
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) || (Keyboard.Modifiers & ModifierKeys.Alt) != 0 || e.Key == Key.System)
                mods.Add("Alt");
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) || (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                mods.Add("Shift");
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin) || (Keyboard.Modifiers & ModifierKeys.Windows) != 0)
                mods.Add("Win");

            string keyName = actualKey switch
            {
                Key.PrintScreen or Key.Snapshot => "Print Screen",
                Key.Escape => "Esc",
                _ => actualKey.ToString()
            };

            string combo = mods.Count > 0 ? $"{string.Join(" + ", mods)} + {keyName}" : keyName;

            var (hasConflict, conflictName) = ShortcutManager.Instance.CheckConflict(combo, _recordingActionId);
            if (hasConflict)
            {
                ShowActionShortcutFeedback($"⚠️ Conflito: O atalho '{combo}' já está em uso pela ação '{conflictName}'. Escolha outra combinação.", isError: true);
                return;
            }

            var actionItem = ShortcutManager.RegisteredActions.FirstOrDefault(a => a.Id == _recordingActionId);
            string actName = actionItem?.DisplayName ?? _recordingActionId;
            ShortcutManager.Instance.SetShortcutForAction(_recordingActionId, combo);
            CancelActionShortcutRecording();
            PopulateActionShortcutsList();
            ShowActionShortcutFeedback($"✓ Atalho da ação '{actName}' atualizado para '{combo}'.", isError: false);
            return;
        }

        if (!_isRecordingHotkey) return;

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            CancelHotkeyRecording();
            ShowHotkeyFeedback("Alteração de atalho cancelada.", isError: false);
            return;
        }

        Key actKey = e.Key == Key.System ? e.SystemKey : e.Key;

        // Se for somente tecla modificadora isolada, continua aguardando a tecla final
        if (actKey is Key.LeftCtrl or Key.RightCtrl or
                          Key.LeftAlt or Key.RightAlt or
                          Key.LeftShift or Key.RightShift or
                          Key.LWin or Key.RWin)
        {
            return;
        }

        var hotkeyMods = new List<string>();
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            hotkeyMods.Add("Ctrl");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) || (Keyboard.Modifiers & ModifierKeys.Alt) != 0 || e.Key == Key.System)
            hotkeyMods.Add("Alt");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) || (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            hotkeyMods.Add("Shift");
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin) || (Keyboard.Modifiers & ModifierKeys.Windows) != 0)
            hotkeyMods.Add("Win");

        string modifierStr = hotkeyMods.Count > 0 ? string.Join(" + ", hotkeyMods) : "None";
        string keyStr = actKey switch
        {
            Key.PrintScreen or Key.Snapshot => "Snapshot",
            _ => actKey.ToString()
        };

        CancelHotkeyRecording();
        ApplyNewHotkey(modifierStr, keyStr);
    }

    private void ApplyNewHotkey(string modifiers, string key)
    {
        var (success, message) = HotkeyService.Instance.TryRegister(modifiers, key, saveToSettings: true);

        var currentSettings = SettingsService.Instance.Current;
        TxtCurrentHotkeyBadge.Text = HotkeyService.FormatHotkeyDisplay(currentSettings.HotkeyModifiers, currentSettings.HotkeyKey);

        SelectModifierInComboBox(currentSettings.HotkeyModifiers);
        SelectKeyInComboBox(currentSettings.HotkeyKey);

        if (success)
        {
            TxtHotkeyStatusText.Text = "✓ Atalho ativo e pronto para uso";
            TxtHotkeyStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            ShowHotkeyFeedback(message, isError: false);
        }
        else
        {
            TxtHotkeyStatusText.Text = "✓ Atalho anterior restaurado";
            TxtHotkeyStatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush");
            ShowHotkeyFeedback(message, isError: true);
        }
    }

    private void ShowHotkeyFeedback(string message, bool isError)
    {
        BorderHotkeyFeedback.Visibility = Visibility.Visible;
        TxtHotkeyFeedback.Text = message;

        if (isError)
        {
            BorderHotkeyFeedback.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xEF, 0x44, 0x44));
            BorderHotkeyFeedback.BorderBrush = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            TxtHotkeyFeedback.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
        }
        else
        {
            BorderHotkeyFeedback.Background = (System.Windows.Media.Brush)FindResource("AccentSubtleBrush");
            BorderHotkeyFeedback.BorderBrush = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            TxtHotkeyFeedback.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        }
    }

    private void BtnRestoreDefaultHotkey_Click(object sender, RoutedEventArgs e)
    {
        CancelHotkeyRecording();
        ApplyNewHotkey("None", "Snapshot");
    }

    private void BtnApplyManualHotkey_Click(object sender, RoutedEventArgs e)
    {
        CancelHotkeyRecording();
        string mod = GetSelectedModifier();
        string key = GetSelectedKey();
        ApplyNewHotkey(mod, key);
    }

    #endregion

    #region Temas Handlers

    private void BtnSelectTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string themeName)
        {
            ThemeService.Instance.ApplyTheme(themeName, persist: true);
            UpdateThemeCardsSelection();
        }
    }

    private void UpdateThemeCardsSelection()
    {
        string current = ThemeService.Instance.CurrentThemeName;

        void SetCard(Border? border, TextBlock? status, bool isActive)
        {
            if (border == null || status == null) return;
            if (isActive)
            {
                border.SetResourceReference(Border.BorderBrushProperty, "AccentBorderBrush");
                border.BorderThickness = new Thickness(2);
                status.Text = "✓ Ativo";
                status.SetResourceReference(TextBlock.ForegroundProperty, "AccentLightBrush");
            }
            else
            {
                border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                border.BorderThickness = new Thickness(1);
                status.Text = string.Empty;
            }
        }

        SetCard(BorderThemePurple, TxtThemeStatusPurple, current.Equals("Purple", StringComparison.OrdinalIgnoreCase));
        SetCard(BorderThemeRed, TxtThemeStatusRed, current.Equals("Red", StringComparison.OrdinalIgnoreCase));
        SetCard(BorderThemePink, TxtThemeStatusPink, current.Equals("Pink", StringComparison.OrdinalIgnoreCase));
        SetCard(BorderThemeGreen, TxtThemeStatusGreen, current.Equals("Green", StringComparison.OrdinalIgnoreCase));
        SetCard(BorderThemeYellow, TxtThemeStatusYellow, current.Equals("Yellow", StringComparison.OrdinalIgnoreCase));
        SetCard(BorderThemeGray, TxtThemeStatusGray, current.Equals("Gray", StringComparison.OrdinalIgnoreCase));
        SetCard(BorderThemeOrange, TxtThemeStatusOrange, current.Equals("Orange", StringComparison.OrdinalIgnoreCase));
        SetCard(BorderThemeBlue, TxtThemeStatusBlue, current.Equals("Blue", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Cursor & Seleção Handlers

    private void BtnCrosshairStyle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn)
        {
            CrosshairStyle style = btn == BtnCrosshairOpen ? CrosshairStyle.Open
                : btn == BtnCrosshairClosed ? CrosshairStyle.Closed
                : btn == BtnCrosshairCenterDot ? CrosshairStyle.CenterDot
                : btn == BtnCrosshairLinesAndDot ? CrosshairStyle.LinesAndDot
                : CrosshairStyle.Simple;

            ApplySelectedCrosshairStyle(style);
        }
    }

    private void CmbCrosshairStyle_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoadingCrosshairUI || CmbCrosshairStyle == null) return;
        int idx = CmbCrosshairStyle.SelectedIndex;
        if (idx >= 0 && Enum.IsDefined(typeof(CrosshairStyle), idx))
        {
            ApplySelectedCrosshairStyle((CrosshairStyle)idx);
        }
    }

    private void ApplySelectedCrosshairStyle(CrosshairStyle style)
    {
        _selectedCrosshairStyle = style;
        _isLoadingCrosshairUI = true;
        try
        {
            SetComboBoxItem(CmbCrosshairStyle, (int)style);
            if (ChkCrosshairShowDot != null)
            {
                if (style is CrosshairStyle.CenterDot or CrosshairStyle.LinesAndDot)
                {
                    ChkCrosshairShowDot.IsChecked = true;
                }
                else if (style == CrosshairStyle.Open)
                {
                    ChkCrosshairShowDot.IsChecked = false;
                }
            }
        }
        finally
        {
            _isLoadingCrosshairUI = false;
        }

        UpdateCrosshairStyleButtons();
        UpdateCursorControlsState();
        RefreshCrosshairLivePreview();
    }

    private void UpdateCrosshairStyleButtons()
    {
        if (BtnCrosshairSimple != null) BtnCrosshairSimple.Tag = _selectedCrosshairStyle == CrosshairStyle.Simple ? "Active" : "Simple";
        if (BtnCrosshairOpen != null) BtnCrosshairOpen.Tag = _selectedCrosshairStyle == CrosshairStyle.Open ? "Active" : "Open";
        if (BtnCrosshairClosed != null) BtnCrosshairClosed.Tag = _selectedCrosshairStyle == CrosshairStyle.Closed ? "Active" : "Closed";
        if (BtnCrosshairCenterDot != null) BtnCrosshairCenterDot.Tag = _selectedCrosshairStyle == CrosshairStyle.CenterDot ? "Active" : "CenterDot";
        if (BtnCrosshairLinesAndDot != null) BtnCrosshairLinesAndDot.Tag = _selectedCrosshairStyle == CrosshairStyle.LinesAndDot ? "Active" : "LinesAndDot";

        // Preserve click identification via button name while using Tag="Active" for visual ChipButtonStyle trigger
        if (TxtCrosshairActiveBadge != null)
        {
            TxtCrosshairActiveBadge.Text = CrosshairService.GetStyleDisplayName(_selectedCrosshairStyle);
        }
        if (TxtCrosshairStyleSubtitle != null)
        {
            TxtCrosshairStyleSubtitle.Text = CrosshairService.GetStyleDescription(_selectedCrosshairStyle);
        }
    }

    private void ChkAutoContrastCursor_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingCrosshairUI) return;
        UpdateCursorControlsState();
        RefreshCrosshairLivePreview();
    }

    private void CrosshairOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingCrosshairUI) return;
        UpdateCursorControlsState();
        RefreshCrosshairLivePreview();
    }

    private void SliderCrosshairGeometry_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtCrosshairThicknessVal != null && SliderCrosshairThickness != null)
            TxtCrosshairThicknessVal.Text = $"{(int)SliderCrosshairThickness.Value} px";
        if (TxtCrosshairLengthVal != null && SliderCrosshairLength != null)
            TxtCrosshairLengthVal.Text = $"{(int)SliderCrosshairLength.Value} px";
        if (TxtCrosshairGapVal != null && SliderCrosshairGap != null)
            TxtCrosshairGapVal.Text = $"{(int)SliderCrosshairGap.Value} px";
        if (TxtCrosshairDotSizeVal != null && SliderCrosshairDotSize != null)
            TxtCrosshairDotSizeVal.Text = $"{(int)SliderCrosshairDotSize.Value} px";

        if (_isLoadingCrosshairUI) return;
        RefreshCrosshairLivePreview();
    }

    private void CmbCursorSize_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoadingCrosshairUI || CmbCursorSize == null || SliderCrosshairLength == null) return;
        SliderCrosshairLength.Value = CmbCursorSize.SelectedIndex switch
        {
            0 => 9,
            2 => 18,
            _ => 12
        };
        RefreshCrosshairLivePreview();
    }

    private void UpdateCursorControlsState()
    {
        if (PanelManualCursorColor == null) return;
        bool isManual = ChkAutoContrastCursor.IsChecked != true;
        PanelManualCursorColor.IsEnabled = isManual;
        PanelManualCursorColor.Opacity = isManual ? 1.0 : 0.48;

        if (ChkCrosshairOutline != null)
        {
            ChkCrosshairOutline.IsEnabled = isManual;
            ChkCrosshairOutline.Opacity = isManual ? 1.0 : 0.55;
        }

        bool isCenterDotOnly = _selectedCrosshairStyle == CrosshairStyle.CenterDot;
        if (SliderCrosshairLength != null) SliderCrosshairLength.IsEnabled = !isCenterDotOnly;
        if (SliderCrosshairThickness != null) SliderCrosshairThickness.IsEnabled = !isCenterDotOnly;
        if (SliderCrosshairGap != null) SliderCrosshairGap.IsEnabled = !isCenterDotOnly && _selectedCrosshairStyle != CrosshairStyle.Closed;

        if (ChkCrosshairShowDot != null)
        {
            bool forcedDot = _selectedCrosshairStyle is CrosshairStyle.CenterDot or CrosshairStyle.LinesAndDot;
            ChkCrosshairShowDot.IsEnabled = !forcedDot;
        }
    }

    private void RefreshCrosshairLivePreview()
    {
        if (_previewDarkBmp == null || _previewLightBmp == null) return;

        var tempSettings = new AppSettings
        {
            CrosshairStyle = _selectedCrosshairStyle,
            AutoContrastCursor = ChkAutoContrastCursor?.IsChecked == true,
            CursorColor = string.IsNullOrWhiteSpace(TxtCursorColorHex?.Text) ? "#8B5CF6" : TxtCursorColorHex.Text.Trim(),
            CrosshairThickness = (int)(SliderCrosshairThickness?.Value ?? 2),
            CrosshairLength = (int)(SliderCrosshairLength?.Value ?? 12),
            CrosshairGap = (int)(SliderCrosshairGap?.Value ?? 4),
            CrosshairDotSize = (int)(SliderCrosshairDotSize?.Value ?? 3),
            CrosshairShowDot = ChkCrosshairShowDot?.IsChecked == true,
            CrosshairOutline = ChkCrosshairOutline?.IsChecked == true
        };

        byte[] mask = CrosshairService.BuildMask(tempSettings);
        var fullPreviewRect = new Rect(0, 0, CrosshairService.GridSize, CrosshairService.GridSize);

        CrosshairService.RenderToBuffer(
            _previewDarkPixels,
            mask,
            tempSettings,
            _simulatedDarkScreen,
            CrosshairService.GridSize,
            CrosshairService.GridSize,
            CrosshairService.GridCenter,
            CrosshairService.GridCenter,
            fullPreviewRect,
            hasSelection: true);

        CrosshairService.RenderToBuffer(
            _previewLightPixels,
            mask,
            tempSettings,
            _simulatedLightScreen,
            CrosshairService.GridSize,
            CrosshairService.GridSize,
            CrosshairService.GridCenter,
            CrosshairService.GridCenter,
            fullPreviewRect,
            hasSelection: true);

        _previewDarkBmp.WritePixels(_previewDirtyRect, _previewDarkPixels, CrosshairService.GridSize * sizeof(int), 0);
        _previewLightBmp.WritePixels(_previewDirtyRect, _previewLightPixels, CrosshairService.GridSize * sizeof(int), 0);
    }

    private void BtnCursorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string hex)
        {
            TxtCursorColorHex.Text = hex;
            UpdateCursorColorPreview(hex);
            RefreshCrosshairLivePreview();
        }
    }

    private void TxtCursorColorHex_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateCursorColorPreview(TxtCursorColorHex.Text);
        if (!_isLoadingCrosshairUI)
        {
            RefreshCrosshairLivePreview();
        }
    }

    private void BtnChooseCustomCursorColor_Click(object sender, RoutedEventArgs e)
    {
        using var cd = new ColorDialog
        {
            FullOpen = true
        };

        try
        {
            var currentMediaColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(TxtCursorColorHex.Text.Trim());
            cd.Color = System.Drawing.Color.FromArgb(currentMediaColor.A, currentMediaColor.R, currentMediaColor.G, currentMediaColor.B);
        }
        catch { }

        if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string hex = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
            TxtCursorColorHex.Text = hex;
            UpdateCursorColorPreview(hex);
            RefreshCrosshairLivePreview();
        }
    }

    private void UpdateCursorColorPreview(string hex)
    {
        if (RectCursorColorPreview == null) return;
        try
        {
            var col = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex.Trim());
            RectCursorColorPreview.Fill = new SolidColorBrush(col);
        }
        catch
        {
            // fallback se texto hex for inválido
        }
    }

    #endregion

    #region Nome dos Arquivos e Variáveis

    private void TxtFilenamePattern_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateFilenamePreview();
    }

    private void TxtSequence_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateFilenamePreview();
    }

    private void CmbSequenceDigits_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateFilenamePreview();
    }

    private void CmbImageFormat_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateFilenamePreview();
    }

    private void BtnPresetFilename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string preset)
        {
            TxtFilenamePattern.Text = preset;
            TxtFilenamePattern.CaretIndex = TxtFilenamePattern.Text.Length;
            TxtFilenamePattern.Focus();
        }
    }

    private void BtnInsertToken_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string token)
        {
            int caret = TxtFilenamePattern.CaretIndex;
            string current = TxtFilenamePattern.Text ?? "";
            if (caret < 0 || caret > current.Length) caret = current.Length;
            TxtFilenamePattern.Text = current.Insert(caret, token);
            TxtFilenamePattern.CaretIndex = caret + token.Length;
            TxtFilenamePattern.Focus();
        }
    }

    private void UpdateFilenamePreview()
    {
        if (TxtFilenamePreview == null || TxtFilenamePattern == null) return;

        string pattern = TxtFilenamePattern.Text ?? "";
        int.TryParse(TxtSequenceCounter?.Text, out int seq);
        if (seq <= 0) seq = 1;
        int digits = (CmbSequenceDigits?.SelectedIndex ?? 0) + 1;

        string ext = (CmbImageFormat?.SelectedIndex == 1) ? "jpg" : "png";

        // Validação de caracteres inválidos no Windows
        var invalid = Path.GetInvalidFileNameChars();
        var foundInvalid = pattern.Where(c => invalid.Contains(c)).Distinct().ToArray();

        if (foundInvalid.Length > 0)
        {
            TxtFilenameValidation.Text = $"⚠ O padrão contém caracteres inválidos ({string.Join(" ", foundInvalid)}). Eles serão convertidos para '_' no arquivo final.";
            TxtFilenameValidation.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
        }
        else if (string.IsNullOrWhiteSpace(pattern))
        {
            TxtFilenameValidation.Text = "⚠ Padrão vazio. O CrankoShot usará o modelo padrão 'Captura_...'.";
            TxtFilenameValidation.Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush");
        }
        else
        {
            TxtFilenameValidation.Text = "✓ Padrão válido para nomes de arquivo do Windows.";
            TxtFilenameValidation.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        }

        // Geração da prévia
        try
        {
            DateTime now = DateTime.Now;
            string preview = ExportService.GenerateFileName(pattern, ext, seq, now);
            TxtFilenamePreview.Text = preview;
        }
        catch
        {
            TxtFilenamePreview.Text = "Captura.png";
        }
    }

    #endregion

    #region Gemini & OpenRouter Models Handling

    private void BtnSelectAiProvider_Click(object sender, RoutedEventArgs e)
    {
        if (sender == BtnProviderGemini)
        {
            _selectedAiProvider = AiProviderType.Gemini;
        }
        else if (sender == BtnProviderOpenRouter)
        {
            _selectedAiProvider = AiProviderType.OpenRouter;
        }

        UpdateAiProviderSelectionUI();
    }

    private void UpdateAiProviderSelectionUI()
    {
        bool isGemini = _selectedAiProvider == AiProviderType.Gemini;

        if (BtnProviderGemini != null)
            BtnProviderGemini.Tag = isGemini ? "Active" : "Gemini";
        if (BtnProviderOpenRouter != null)
            BtnProviderOpenRouter.Tag = !isGemini ? "Active" : "OpenRouter";

        if (TxtActiveAiProviderBadge != null)
            TxtActiveAiProviderBadge.Text = isGemini ? "✦ Google Gemini" : "🌐 OpenRouter";

        if (BadgeGeminiActiveState != null && TxtGeminiActiveState != null)
        {
            BadgeGeminiActiveState.Visibility = Visibility.Visible;
            TxtGeminiActiveState.Text = isGemini ? "PROVEDOR ATIVO" : "DISPONÍVEL";
            BadgeGeminiActiveState.SetResourceReference(Border.BackgroundProperty, isGemini ? "AccentSubtleBrush" : "CardHoverBrush");
            BadgeGeminiActiveState.SetResourceReference(Border.BorderBrushProperty, isGemini ? "AccentBorderBrush" : "BorderBrush");
            TxtGeminiActiveState.SetResourceReference(TextBlock.ForegroundProperty, isGemini ? "AccentLightBrush" : "TextSecondaryBrush");
        }

        if (BadgeOpenRouterActiveState != null && TxtOpenRouterActiveState != null)
        {
            BadgeOpenRouterActiveState.Visibility = Visibility.Visible;
            TxtOpenRouterActiveState.Text = !isGemini ? "PROVEDOR ATIVO" : "DISPONÍVEL";
            BadgeOpenRouterActiveState.SetResourceReference(Border.BackgroundProperty, !isGemini ? "AccentSubtleBrush" : "CardHoverBrush");
            BadgeOpenRouterActiveState.SetResourceReference(Border.BorderBrushProperty, !isGemini ? "AccentBorderBrush" : "BorderBrush");
            TxtOpenRouterActiveState.SetResourceReference(TextBlock.ForegroundProperty, !isGemini ? "AccentLightBrush" : "TextSecondaryBrush");
        }

        if (CardGeminiSettings != null)
        {
            CardGeminiSettings.SetResourceReference(Border.BorderBrushProperty, isGemini ? "AccentBorderBrush" : "BorderBrush");
        }

        if (CardOpenRouterSettings != null)
        {
            CardOpenRouterSettings.SetResourceReference(Border.BorderBrushProperty, !isGemini ? "AccentBorderBrush" : "BorderBrush");
        }
    }

    private void LoadInitialModels(string selectedModel)
    {
        _availableModels.Clear();
        _availableModels.AddRange(GeminiService.ApprovedModels);

        string cleanSelected = GeminiService.NormalizeModelName(selectedModel);
        if (!_availableModels.Any(m => m.Id.Equals(cleanSelected, StringComparison.OrdinalIgnoreCase)))
        {
            _availableModels.Insert(0, new GeminiModelInfo(cleanSelected, cleanSelected, "Modelo configurado atualmente pelo usuário."));
        }

        UpdateModelComboBox(cleanSelected);
    }

    private void UpdateModelComboBox(string selectedId)
    {
        CmbGeminiModel.ItemsSource = null;
        CmbGeminiModel.DisplayMemberPath = "DisplayName";
        CmbGeminiModel.SelectedValuePath = "Id";
        CmbGeminiModel.ItemsSource = _availableModels;

        var selected = _availableModels.FirstOrDefault(m => m.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                       ?? _availableModels.FirstOrDefault();

        if (selected != null)
        {
            CmbGeminiModel.SelectedItem = selected;
            TxtModelDescription.Text = selected.Description;
        }
    }

    private void CmbGeminiModel_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CmbGeminiModel.SelectedItem is GeminiModelInfo model)
        {
            TxtModelDescription.Text = string.IsNullOrEmpty(model.Description) ? $"ID do modelo: {model.Id}" : model.Description;
        }
    }

    private void LoadInitialOpenRouterModels(string selectedModel, bool freeOnly)
    {
        _availableOpenRouterModels.Clear();
        var defaults = freeOnly
            ? OpenRouterService.DefaultModels.Where(m => m.IsFree)
            : OpenRouterService.DefaultModels;
        _availableOpenRouterModels.AddRange(defaults);

        string cleanSelected = OpenRouterService.NormalizeModelName(selectedModel);
        if (!_availableOpenRouterModels.Any(m => m.Id.Equals(cleanSelected, StringComparison.OrdinalIgnoreCase)))
        {
            var known = OpenRouterService.Instance.GetModelMetadata(cleanSelected);
            if (known != null)
            {
                _availableOpenRouterModels.Insert(0, known);
            }
            else
            {
                bool isFreeId = cleanSelected.EndsWith(":free", StringComparison.OrdinalIgnoreCase)
                                || cleanSelected.Equals("openrouter/free", StringComparison.OrdinalIgnoreCase);
                _availableOpenRouterModels.Insert(0, new OpenRouterModelInfo(
                    cleanSelected,
                    cleanSelected,
                    $"{cleanSelected} (Personalizado)",
                    "Modelo OpenRouter configurado atualmente pelo usuário.",
                    isFree: isFreeId,
                    supportsVision: true,
                    supportsText: true));
            }
        }

        UpdateOpenRouterModelComboBox(cleanSelected);
    }

    private void UpdateOpenRouterModelComboBox(string selectedId)
    {
        if (CmbOpenRouterModel == null) return;

        CmbOpenRouterModel.ItemsSource = null;
        CmbOpenRouterModel.DisplayMemberPath = "DisplayName";
        CmbOpenRouterModel.SelectedValuePath = "Id";
        CmbOpenRouterModel.ItemsSource = _availableOpenRouterModels;

        var selected = _availableOpenRouterModels.FirstOrDefault(m => m.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                       ?? _availableOpenRouterModels.FirstOrDefault();

        if (selected != null)
        {
            CmbOpenRouterModel.SelectedItem = selected;
            UpdateOpenRouterModelDetailsUI(selected);
        }
    }

    private void CmbOpenRouterModel_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CmbOpenRouterModel?.SelectedItem is OpenRouterModelInfo model)
        {
            UpdateOpenRouterModelDetailsUI(model);
        }
    }

    private void UpdateOpenRouterModelDetailsUI(OpenRouterModelInfo model)
    {
        if (TxtOpenRouterModelDescription != null)
        {
            TxtOpenRouterModelDescription.Text = string.IsNullOrWhiteSpace(model.Description)
                ? $"ID do modelo: {model.Id}"
                : $"{model.Description} (ID: {model.Id})";
        }

        if (TxtOpenRouterModalityBadge != null)
        {
            TxtOpenRouterModalityBadge.Text = model.SupportsVision
                ? "👁️ Modalidade: Visão + Texto (compatível com análise direta de imagens)"
                : "📝 Modalidade: Somente Texto (usa OCR local automático quando acionado sobre capturas)";
        }

        if (TxtOpenRouterPricingBadge != null)
        {
            TxtOpenRouterPricingBadge.Text = model.IsFree
                ? "✓ Gratuito ($0/token)"
                : "💲 Modelo Pago (consome créditos OpenRouter)";
            TxtOpenRouterPricingBadge.SetResourceReference(
                TextBlock.ForegroundProperty,
                model.IsFree ? "SuccessBrush" : "AccentLightBrush");
        }
    }

    private async void ChkOpenRouterFreeOnly_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingAiUI) return;
        bool freeOnly = ChkOpenRouterFreeOnly?.IsChecked == true;
        string currentId = GetSelectedOpenRouterModel();
        await RefreshOpenRouterCatalogInternalAsync(freeOnly, currentId, showStatusMessage: false);
    }

    private async void BtnRefreshOpenRouterModels_Click(object sender, RoutedEventArgs e)
    {
        bool freeOnly = ChkOpenRouterFreeOnly?.IsChecked == true;
        string currentId = GetSelectedOpenRouterModel();
        await RefreshOpenRouterCatalogInternalAsync(freeOnly, currentId, showStatusMessage: true);
    }

    private async Task RefreshOpenRouterCatalogInternalAsync(bool freeOnly, string preferredModelId, bool showStatusMessage)
    {
        if (BtnRefreshOpenRouterModels != null)
            BtnRefreshOpenRouterModels.IsEnabled = false;

        try
        {
            if (showStatusMessage && TxtOpenRouterApiKeyStatus != null)
            {
                TxtOpenRouterApiKeyStatus.Text = "Consultando catálogo oficial de modelos do OpenRouter...";
                TxtOpenRouterApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("PurplePrimaryBrush");
            }

            var (ok, models, statusMsg) = await OpenRouterService.Instance.GetAvailableModelsAsync(freeOnly);
            _availableOpenRouterModels.Clear();
            _availableOpenRouterModels.AddRange(models);

            UpdateOpenRouterModelComboBox(preferredModelId);

            if (showStatusMessage && TxtOpenRouterApiKeyStatus != null)
            {
                TxtOpenRouterApiKeyStatus.Text = ok ? $"✓ {statusMsg}" : statusMsg;
                TxtOpenRouterApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource(ok ? "SuccessBrush" : "TextMutedBrush");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao atualizar catálogo de modelos do OpenRouter na UI", ex);
            if (showStatusMessage && TxtOpenRouterApiKeyStatus != null)
            {
                TxtOpenRouterApiKeyStatus.Text = "Não foi possível consultar a lista online. Mantendo catálogo verificado integrado.";
                TxtOpenRouterApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            }
        }
        finally
        {
            if (BtnRefreshOpenRouterModels != null)
                BtnRefreshOpenRouterModels.IsEnabled = true;
        }
    }

    private string GetSelectedOpenRouterModel()
    {
        if (CmbOpenRouterModel?.SelectedItem is OpenRouterModelInfo model)
        {
            return model.Id;
        }
        return OpenRouterService.NormalizeModelName(SettingsService.Instance.Current.OpenRouterModel);
    }

    private async void BtnTestOpenRouterApi_Click(object sender, RoutedEventArgs e)
    {
        string inputKey = TxtOpenRouterApiKey.Password.Trim();
        string keyToTest = string.IsNullOrEmpty(inputKey) ? SettingsService.Instance.GetOpenRouterApiKey() : inputKey;

        if (string.IsNullOrEmpty(keyToTest))
        {
            System.Windows.MessageBox.Show("Digite uma chave de API do OpenRouter para testar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string model = GetSelectedOpenRouterModel();
        BtnTestOpenRouterApi.IsEnabled = false;
        TxtOpenRouterApiKeyStatus.Text = $"Testando conexão com o modelo '{model}' no OpenRouter...";
        TxtOpenRouterApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("PurplePrimaryBrush");

        var (success, message) = await OpenRouterService.Instance.TestConnectionAsync(keyToTest, model);
        BtnTestOpenRouterApi.IsEnabled = true;

        if (success)
        {
            TxtOpenRouterApiKeyStatus.Text = message;
            TxtOpenRouterApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            System.Windows.MessageBox.Show(message, "Conexão OpenRouter Bem-Sucedida", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            TxtOpenRouterApiKeyStatus.Text = message;
            TxtOpenRouterApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            System.Windows.MessageBox.Show(message, "Falha na Conexão OpenRouter", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    private void LoadOcrInfo()
    {
        if (OcrService.IsOcrAvailable())
        {
            var langs = OcrService.GetAvailableLanguages();
            TxtOcrStatus.Text = "Mecanismo Windows.Media.Ocr ativo e operacional no sistema.";
            TxtOcrStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            TxtOcrLanguages.Text = $"Idiomas disponíveis instalados: {string.Join(", ", langs)}";
        }
        else
        {
            TxtOcrStatus.Text = "Mecanismo de OCR local não disponível ou sem idiomas instalados no Windows.";
            TxtOcrStatus.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            TxtOcrLanguages.Text = "Instale um pacote de idioma com OCR nas configurações do Windows.";
        }
    }

    private static void SetComboBoxItem(System.Windows.Controls.ComboBox cb, int index)
    {
        if (cb.Items.Count > index && index >= 0)
        {
            cb.SelectedIndex = index;
        }
    }

    private void BtnBrowseDirectory_Click(object sender, RoutedEventArgs e)
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Selecione a pasta padrão para salvar as capturas",
            SelectedPath = TxtSaveDirectory.Text
        };

        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtSaveDirectory.Text = fbd.SelectedPath;
        }
    }

    private async void BtnTestApi_Click(object sender, RoutedEventArgs e)
    {
        string inputKey = TxtGeminiApiKey.Password.Trim();
        string keyToTest = string.IsNullOrEmpty(inputKey) ? SettingsService.Instance.GetApiKey() : inputKey;

        if (string.IsNullOrEmpty(keyToTest))
        {
            System.Windows.MessageBox.Show("Digite uma chave de API para testar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string model = GetSelectedModel();
        BtnTestApi.IsEnabled = false;
        TxtApiKeyStatus.Text = $"Testando conexão com o modelo '{model}' no Google Gemini...";
        TxtApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("PurplePrimaryBrush");

        var (success, message) = await GeminiService.Instance.TestConnectionAsync(keyToTest, model);
        BtnTestApi.IsEnabled = true;

        if (success)
        {
            TxtApiKeyStatus.Text = message;
            TxtApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            System.Windows.MessageBox.Show(message, "Conexão Bem-Sucedida", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            TxtApiKeyStatus.Text = message;
            TxtApiKeyStatus.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            System.Windows.MessageBox.Show(message, "Falha na Conexão", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetSelectedModel()
    {
        if (CmbGeminiModel.SelectedItem is GeminiModelInfo model)
        {
            return model.Id;
        }
        return GeminiService.NormalizeModelName(SettingsService.Instance.Current.GeminiModel);
    }

    private async void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (BtnSaveSettings.IsEnabled == false) return;
        BtnSaveSettings.IsEnabled = false;

        try
        {
            var s = SettingsService.Instance.Current;

            // Geral
            bool runOnStartup = ChkRunOnStartup.IsChecked == true;
            s.RunOnStartup = runOnStartup;
            StartupService.SetRunOnStartup(runOnStartup);

            s.MinimizeToTray = ChkMinimizeToTray.IsChecked == true;
            s.PlaySoundOnCapture = ChkPlaySound.IsChecked == true;
            s.ShowNotifications = ChkNotifications.IsChecked == true;

            // Atalhos
            string selectedMod = GetSelectedModifier();
            string selectedKey = GetSelectedKey();
            if (selectedMod != s.HotkeyModifiers || selectedKey != s.HotkeyKey)
            {
                var regResult = HotkeyService.Instance.TryRegister(selectedMod, selectedKey, saveToSettings: true);
                if (!regResult.success)
                {
                    System.Windows.MessageBox.Show(regResult.message, "Aviso sobre Atalho", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                HotkeyService.Instance.RegisterConfiguredHotkey();
            }

            // Windows Snipping Override
            bool overrideSnipping = ChkOverrideWindowsSnipping.IsChecked == true;
            if (overrideSnipping != s.OverrideWindowsSnippingTool)
            {
                s.OverrideWindowsSnippingTool = overrideSnipping;
                bool success = await Task.Run(() => WindowsSnippingService.SetSnippingOverride(overrideSnipping));
                if (!success)
                {
                    System.Windows.MessageBox.Show("Não foi possível salvar a substituição do Print Screen no registro do Windows. Verifique se possui permissões administrativas.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Captura
            s.DefaultAction = (DefaultCaptureAction)CmbDefaultAction.SelectedIndex;
            s.DefaultSaveDirectory = TxtSaveDirectory.Text.Trim();
            s.DefaultFormat = (ImageFormatType)CmbImageFormat.SelectedIndex;

            if (int.TryParse(TxtJpgQuality.Text, out int quality))
            {
                s.JpgQuality = Math.Clamp(quality, 10, 100);
            }

            s.FilenamePattern = string.IsNullOrWhiteSpace(TxtFilenamePattern.Text) ? "ShotMind_{yyyy-MM-dd_HH-mm-ss}" : TxtFilenamePattern.Text.Trim();
            if (int.TryParse(TxtSequenceCounter.Text, out int sequence) && sequence >= 0)
            {
                s.SequenceCounter = sequence;
            }
            s.SequenceDigits = Math.Clamp(CmbSequenceDigits.SelectedIndex + 1, 1, 6);

            // Censura / Ocultação
            s.DefaultObfuscationStyle = (ObfuscationStyle)Math.Max(0, CmbObfuscationStyle.SelectedIndex);
            if (int.TryParse(TxtObfuscationIntensity.Text, out int intensity))
            {
                s.ObfuscationIntensity = Math.Clamp(intensity, 4, 64);
            }

            // Cursor & Seleção (Mira)
            s.CrosshairStyle = _selectedCrosshairStyle;
            s.AutoContrastCursor = ChkAutoContrastCursor.IsChecked == true;
            s.CrosshairOutline = ChkCrosshairOutline.IsChecked == true;
            s.CrosshairShowDot = ChkCrosshairShowDot.IsChecked == true;
            s.CrosshairThickness = (int)Math.Round(SliderCrosshairThickness.Value);
            s.CrosshairLength = (int)Math.Round(SliderCrosshairLength.Value);
            s.CrosshairGap = (int)Math.Round(SliderCrosshairGap.Value);
            s.CrosshairDotSize = (int)Math.Round(SliderCrosshairDotSize.Value);

            string hex = TxtCursorColorHex.Text.Trim();
            if (!string.IsNullOrEmpty(hex))
            {
                s.CursorColor = hex;
            }
            s.CursorSize = CmbCursorSize.SelectedIndex switch
            {
                0 => 14,
                2 => 24,
                _ => 18
            };

            // IA
            s.AiProvider = _selectedAiProvider;

            string newApiKey = TxtGeminiApiKey.Password.Trim();
            if (!string.IsNullOrEmpty(newApiKey))
            {
                SettingsService.Instance.SetApiKey(newApiKey);
            }

            string newOpenRouterKey = TxtOpenRouterApiKey.Password.Trim();
            if (!string.IsNullOrEmpty(newOpenRouterKey))
            {
                SettingsService.Instance.SetOpenRouterApiKey(newOpenRouterKey);
            }

            s.GeminiModel = GetSelectedModel();
            s.OpenRouterModel = GetSelectedOpenRouterModel();
            s.OpenRouterFreeModelsOnly = ChkOpenRouterFreeOnly.IsChecked == true;
            s.AutoAnalyzeEnabled = ChkAutoAnalyze.IsChecked == true;

            // Tema de Cores
            s.AccentTheme = ThemeService.Instance.CurrentThemeName;

            // Salvar persistência
            SettingsService.Instance.Save();

            System.Windows.MessageBox.Show("Configurações salvas com sucesso!", "CrankoShot", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao salvar configurações", ex);
            System.Windows.MessageBox.Show($"Ocorreu um erro ao salvar as configurações: {ex.Message}", "Erro ao Salvar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSaveSettings.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
