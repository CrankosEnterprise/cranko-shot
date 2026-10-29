using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ShotMind.Core.Models;

namespace ShotMind.Core.Services;

public record ShortcutItem(
    string Id,
    string DisplayName,
    string Description,
    string DefaultShortcut,
    bool IsGlobal
);

public class ShortcutManager
{
    private static ShortcutManager? _instance;
    public static ShortcutManager Instance => _instance ??= new ShortcutManager();

    public const string ActionCapture = "Capture";
    public const string ActionCopy = "Copy";
    public const string ActionSave = "Save";
    public const string ActionEditor = "Editor";
    public const string ActionAi = "Ai";
    public const string ActionOcr = "Ocr";
    public const string ActionPin = "Pin";
    public const string ActionUndo = "Undo";
    public const string ActionRedo = "Redo";
    public const string ActionCancel = "Cancel";
    public const string ActionCloseEditor = "CloseEditor";

    public static readonly IReadOnlyList<ShortcutItem> RegisteredActions = new[]
    {
        new ShortcutItem("Capture", "Iniciar captura", "Abre o overlay para selecionar e capturar qualquer área da tela.", "Print Screen", true),
        new ShortcutItem("Copy", "Copiar captura atual", "Copia a seleção com todas as anotações para a área de transferência.", "Ctrl + C", false),
        new ShortcutItem("Save", "Salvar captura atual", "Abre a caixa de diálogo para salvar a imagem no disco.", "Ctrl + S", false),
        new ShortcutItem("Editor", "Abrir editor", "Abre a captura no editor completo de imagens.", "Ctrl + E", false),
        new ShortcutItem("Ai", "Abrir ações de IA", "Inicia o chat ou menu inteligente com a IA Gemini.", "Ctrl + I", false),
        new ShortcutItem("Ocr", "Extrair texto por OCR", "Extrai todo o texto contido na área selecionada.", "Ctrl + Shift + O", false),
        new ShortcutItem("Pin", "Fixar captura na tela", "Cria uma janela flutuante independente sempre visível no topo.", "Ctrl + Shift + P", false),
        new ShortcutItem("Undo", "Desfazer anotação", "Desfaz a última alteração ou desenho feito na captura.", "Ctrl + Z", false),
        new ShortcutItem("Redo", "Refazer anotação", "Restaura a anotação desfeita anteriormente.", "Ctrl + Y", false),
        new ShortcutItem("Cancel", "Cancelar / Fechar ativo", "Cancela a seleção atual ou fecha o elemento ativo sem fechar o app.", "Esc", false),
        new ShortcutItem("CloseEditor", "Fechar editor atual", "Fecha a janela do editor ou captura em foco.", "Ctrl + W", false)
    };

    private ShortcutManager() { }

    public string GetShortcutForAction(string actionId)
    {
        var s = SettingsService.Instance.Current;
        return actionId switch
        {
            "Capture" => HotkeyService.FormatHotkeyDisplay(s.HotkeyModifiers, s.HotkeyKey),
            "Copy" => s.ShortcutCopy,
            "Save" => s.ShortcutSave,
            "Editor" => s.ShortcutEditor,
            "Ai" => s.ShortcutAi,
            "Ocr" => s.ShortcutOcr,
            "Pin" => s.ShortcutPin,
            "Undo" => s.ShortcutUndo,
            "Redo" => s.ShortcutRedo,
            "Cancel" => s.ShortcutCancel,
            "CloseEditor" => s.ShortcutCloseEditor,
            _ => string.Empty
        };
    }

    public void SetShortcutForAction(string actionId, string shortcut)
    {
        var s = SettingsService.Instance.Current;
        switch (actionId)
        {
            case "Copy": s.ShortcutCopy = shortcut; break;
            case "Save": s.ShortcutSave = shortcut; break;
            case "Editor": s.ShortcutEditor = shortcut; break;
            case "Ai": s.ShortcutAi = shortcut; break;
            case "Ocr": s.ShortcutOcr = shortcut; break;
            case "Pin": s.ShortcutPin = shortcut; break;
            case "Undo": s.ShortcutUndo = shortcut; break;
            case "Redo": s.ShortcutRedo = shortcut; break;
            case "Cancel": s.ShortcutCancel = shortcut; break;
            case "CloseEditor": s.ShortcutCloseEditor = shortcut; break;
        }
    }

    public void RestoreDefault(string actionId)
    {
        var def = RegisteredActions.FirstOrDefault(a => a.Id == actionId);
        if (def != null)
        {
            SetShortcutForAction(actionId, def.DefaultShortcut);
        }
    }

    public void RestoreAllDefaults()
    {
        foreach (var a in RegisteredActions)
        {
            if (!a.IsGlobal)
            {
                SetShortcutForAction(a.Id, a.DefaultShortcut);
            }
        }
    }

    public bool IsActionTriggered(KeyEventArgs e, string actionId)
    {
        string shortcut = GetShortcutForAction(actionId);
        return Matches(e, shortcut);
    }

    public bool IsActionTriggered(string actionId, KeyEventArgs e, AppSettings? settings = null)
    {
        string shortcut = settings != null
            ? actionId switch
            {
                ActionCapture => HotkeyService.FormatHotkeyDisplay(settings.HotkeyModifiers, settings.HotkeyKey),
                ActionCopy => settings.ShortcutCopy,
                ActionSave => settings.ShortcutSave,
                ActionEditor => settings.ShortcutEditor,
                ActionAi => settings.ShortcutAi,
                ActionOcr => settings.ShortcutOcr,
                ActionPin => settings.ShortcutPin,
                ActionUndo => settings.ShortcutUndo,
                ActionRedo => settings.ShortcutRedo,
                ActionCancel => settings.ShortcutCancel,
                ActionCloseEditor => settings.ShortcutCloseEditor,
                _ => GetShortcutForAction(actionId)
            }
            : GetShortcutForAction(actionId);

        return Matches(e, shortcut);
    }

    public static bool Matches(KeyEventArgs e, string? shortcutStr)
    {
        if (string.IsNullOrWhiteSpace(shortcutStr)) return false;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        // Trata teclas modificadoras sozinhas
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return false;
        }

        string clean = shortcutStr.Trim();

        // Caso especial: Esc / Escape
        if (clean.Equals("Esc", StringComparison.OrdinalIgnoreCase) || clean.Equals("Escape", StringComparison.OrdinalIgnoreCase))
        {
            return key == Key.Escape && modifiers == ModifierKeys.None;
        }

        // Caso especial: Print Screen
        if (clean.Equals("Print Screen", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("Snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return (key == Key.Snapshot || key == Key.PrintScreen) && modifiers == ModifierKeys.None;
        }

        // Divide partes (modificadores + tecla principal)
        var parts = clean.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(p => p.Trim())
                         .ToList();

        if (parts.Count == 0) return false;

        bool needCtrl = parts.Any(p => p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase));
        bool needAlt = parts.Any(p => p.Equals("Alt", StringComparison.OrdinalIgnoreCase));
        bool needShift = parts.Any(p => p.Equals("Shift", StringComparison.OrdinalIgnoreCase));
        bool needWin = parts.Any(p => p.Equals("Win", StringComparison.OrdinalIgnoreCase) || p.Equals("Windows", StringComparison.OrdinalIgnoreCase));

        // A última parte é a tecla principal
        string mainKeyPart = parts.Last();

        bool hasCtrl = (modifiers & ModifierKeys.Control) != 0;
        bool hasAlt = (modifiers & ModifierKeys.Alt) != 0;
        bool hasShift = (modifiers & ModifierKeys.Shift) != 0;
        bool hasWin = (modifiers & ModifierKeys.Windows) != 0;

        if (needCtrl != hasCtrl || needAlt != hasAlt || needShift != hasShift || needWin != hasWin)
        {
            return false;
        }

        return KeyMatchesString(key, mainKeyPart);
    }

    private static bool KeyMatchesString(Key key, string keyPart)
    {
        if (keyPart.Equals("Print Screen", StringComparison.OrdinalIgnoreCase) ||
            keyPart.Equals("Snapshot", StringComparison.OrdinalIgnoreCase) ||
            keyPart.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase))
        {
            return key == Key.Snapshot || key == Key.PrintScreen;
        }

        if (keyPart.Equals("Esc", StringComparison.OrdinalIgnoreCase) ||
            keyPart.Equals("Escape", StringComparison.OrdinalIgnoreCase))
        {
            return key == Key.Escape;
        }

        if (Enum.TryParse<Key>(keyPart, true, out var parsedKey))
        {
            return key == parsedKey;
        }

        // Letras e números
        if (keyPart.Length == 1)
        {
            char c = char.ToUpperInvariant(keyPart[0]);
            if (c >= 'A' && c <= 'Z')
            {
                return key.ToString().Equals(c.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            if (c >= '0' && c <= '9')
            {
                return key.ToString().Equals($"D{c}", StringComparison.OrdinalIgnoreCase) ||
                       key.ToString().Equals($"NumPad{c}", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    public (bool hasConflict, string conflictingActionName) CheckConflict(string newShortcut, string currentActionId)
    {
        string norm = NormalizeShortcutString(newShortcut);
        foreach (var action in RegisteredActions)
        {
            if (action.Id.Equals(currentActionId, StringComparison.OrdinalIgnoreCase)) continue;

            string existing = NormalizeShortcutString(GetShortcutForAction(action.Id));
            if (!string.IsNullOrEmpty(existing) && existing.Equals(norm, StringComparison.OrdinalIgnoreCase))
            {
                return (true, action.DisplayName);
            }
        }

        return (false, string.Empty);
    }

    public static string NormalizeShortcutString(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return string.Empty;

        string trimmed = shortcut.Trim();
        if (trimmed.Equals("Print Screen", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return "Print Screen";
        }
        if (trimmed.Equals("Esc", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Escape", StringComparison.OrdinalIgnoreCase))
        {
            return "Esc";
        }

        var parts = trimmed.Split('+', StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p.Trim())
                           .ToList();
        var mods = new List<string>();
        string mainKey = string.Empty;

        foreach (var p in parts)
        {
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase))
                mods.Add("Ctrl");
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                mods.Add("Alt");
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mods.Add("Shift");
            else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase) || p.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                mods.Add("Win");
            else
                mainKey = p;
        }

        int GetModRank(string m) => m switch
        {
            "Ctrl" => 1,
            "Alt" => 2,
            "Shift" => 3,
            "Win" => 4,
            _ => 5
        };
        mods = mods.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(GetModRank).ToList();

        if (!string.IsNullOrEmpty(mainKey))
        {
            if (mainKey.Length == 1)
                mainKey = mainKey.ToUpperInvariant();
            else if (mainKey.Equals("Esc", StringComparison.OrdinalIgnoreCase) || mainKey.Equals("Escape", StringComparison.OrdinalIgnoreCase))
                mainKey = "Esc";
            else if (mainKey.Equals("Print Screen", StringComparison.OrdinalIgnoreCase) || mainKey.Equals("Snapshot", StringComparison.OrdinalIgnoreCase) || mainKey.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase))
                mainKey = "Print Screen";

            mods.Add(mainKey);
        }

        return string.Join(" + ", mods);
    }
}
