using System;
using System.Collections.Generic;
using System.IO;

namespace ShotMind.Core.Models;

public class AppSettings
{
    // Geral
    public bool RunOnStartup { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool PlaySoundOnCapture { get; set; } = true;

    // Som da captura: "system" (som do Windows), "builtin:<nome>" ou "custom:<id>"
    public string CaptureSoundId { get; set; } = "system";
    public int CaptureSoundVolume { get; set; } = 100; // 0..100, independente do volume geral do Windows
    public bool CaptureSoundMuted { get; set; } = false;
    public List<CustomCaptureSound> CustomCaptureSounds { get; set; } = new();

    public bool ShowNotifications { get; set; } = true;
    public bool DarkTheme { get; set; } = true;
    public string AccentTheme { get; set; } = "Purple"; // Purple, Red, Pink, Green, Yellow, White, Orange, Blue

    // Atalhos Globais e Contextuais
    public string HotkeyKey { get; set; } = "Snapshot"; // Snapshot = PrintScreen
    public string HotkeyModifiers { get; set; } = "None"; // None, Ctrl, Alt, Shift
    public string ShortcutCopy { get; set; } = "Ctrl + C";
    public string ShortcutSave { get; set; } = "Ctrl + S";
    public string ShortcutEditor { get; set; } = "Ctrl + E";
    public string ShortcutAi { get; set; } = "Ctrl + I";
    public string ShortcutOcr { get; set; } = "Ctrl + Shift + O";
    public string ShortcutPin { get; set; } = "Ctrl + Shift + P";
    public string ShortcutUndo { get; set; } = "Ctrl + Z";
    public string ShortcutRedo { get; set; } = "Ctrl + Y";
    public string ShortcutCancel { get; set; } = "Esc";
    public string ShortcutCloseEditor { get; set; } = "Ctrl + W";

    // Marcadores Sequenciais
    public StepMarkerStyle DefaultMarkerStyle { get; set; } = StepMarkerStyle.Numbers;
    public double DefaultMarkerSize { get; set; } = 26.0;
    public string DefaultMarkerColor { get; set; } = "#8B5CF6";
    public string DefaultMarkerTextColor { get; set; } = "#FFFFFF";
    public bool DefaultMarkerHasBorder { get; set; } = true;

    // Captura e Salvamento
    public DefaultCaptureAction DefaultAction { get; set; } = DefaultCaptureAction.CopyToClipboard;
    public string DefaultSaveDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShotMind");
    public ImageFormatType DefaultFormat { get; set; } = ImageFormatType.Png;
    public int JpgQuality { get; set; } = 92;
    public string FilenamePattern { get; set; } = "ShotMind_{yyyy-MM-dd_HH-mm-ss}";
    public int SequenceCounter { get; set; } = 1;
    public int SequenceDigits { get; set; } = 3;
    public bool CloseOverlayAfterAction { get; set; } = true;
    public ObfuscationStyle DefaultObfuscationStyle { get; set; } = ObfuscationStyle.Pixelate;
    public int ObfuscationIntensity { get; set; } = 12;
    public bool OverrideWindowsSnippingTool { get; set; } = false;

    // Cursor e Seleção (Mira)
    public bool AutoContrastCursor { get; set; } = true;
    public string CursorColor { get; set; } = "#8B5CF6";
    public int CursorSize { get; set; } = 18; // Escala: 14 = Pequena, 18 = Normal, 24 = Grande, 0 = Personalizado
    public int CrosshairScaleVersion { get; set; } = 2; // 2 = presets completos de 4 dimensões
    public CrosshairStyle CrosshairStyle { get; set; } = CrosshairStyle.Simple;
    public int CrosshairThickness { get; set; } = 2;
    public int CrosshairLength { get; set; } = 12;
    public int CrosshairGap { get; set; } = 4;
    public int CrosshairDotSize { get; set; } = 3;
    public bool CrosshairShowDot { get; set; } = false;
    public bool CrosshairOutline { get; set; } = true;

    // IA (Gemini & OpenRouter)
    public AiProviderType AiProvider { get; set; } = AiProviderType.Gemini;
    public string EncryptedGeminiApiKey { get; set; } = string.Empty;
    public string GeminiModel { get; set; } = "gemini-3.5-flash";
    public string EncryptedOpenRouterApiKey { get; set; } = string.Empty;
    public string OpenRouterModel { get; set; } = "google/gemma-4-31b-it:free";
    public bool OpenRouterFreeModelsOnly { get; set; } = true;

    // OCR
    public string PreferredOcrLanguage { get; set; } = "pt-BR";
}
