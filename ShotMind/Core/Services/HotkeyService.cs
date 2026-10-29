using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ShotMind.Core.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9001;

    // Modifiers Win32
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private bool _isRegistered = false;

    public bool IsRegistered => _isRegistered;

    public event Action? HotkeyPressed;
    public event Action<string>? HotkeyRegistrationFailed;

    private static HotkeyService? _instance;
    public static HotkeyService Instance => _instance ??= new HotkeyService();

    private HotkeyService()
    {
    }

    public void Initialize()
    {
        if (_source != null) return;

        var parameters = new HwndSourceParameters("ShotMindHotkeyListener")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(HwndHook);

        RegisterConfiguredHotkey();
    }

    public static uint GetModifierFlags(string? modifiers)
    {
        uint flags = MOD_NOREPEAT;
        if (string.IsNullOrWhiteSpace(modifiers) || modifiers.Equals("None", StringComparison.OrdinalIgnoreCase) || modifiers.Equals("Nenhum", StringComparison.OrdinalIgnoreCase))
            return flags;

        if (modifiers.Contains("Ctrl", StringComparison.OrdinalIgnoreCase) || modifiers.Contains("Control", StringComparison.OrdinalIgnoreCase))
            flags |= MOD_CONTROL;
        if (modifiers.Contains("Alt", StringComparison.OrdinalIgnoreCase))
            flags |= MOD_ALT;
        if (modifiers.Contains("Shift", StringComparison.OrdinalIgnoreCase))
            flags |= MOD_SHIFT;
        if (modifiers.Contains("Win", StringComparison.OrdinalIgnoreCase) || modifiers.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            flags |= MOD_WIN;

        return flags;
    }

    public static uint GetVirtualKey(string? keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
            return 0x2C; // VK_SNAPSHOT (Print Screen)

        string clean = keyString.Trim();

        if (clean.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("Print Screen", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("Snapshot", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("PrtScn", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("PrtSc", StringComparison.OrdinalIgnoreCase))
        {
            return 0x2C; // VK_SNAPSHOT
        }

        if (Enum.TryParse<Key>(clean, true, out var key))
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk > 0) return (uint)vk;
        }

        return 0x2C;
    }

    public static string FormatKeyName(string? keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
            return "Print Screen";

        string clean = keyString.Trim();
        if (clean.Equals("Snapshot", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("Print Screen", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("PrtScn", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("PrtSc", StringComparison.OrdinalIgnoreCase))
        {
            return "Print Screen";
        }

        return clean.ToUpperInvariant();
    }

    public static string FormatHotkeyDisplay(string? modifiers, string? keyString)
    {
        string keyName = FormatKeyName(keyString);
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(modifiers) && !modifiers.Equals("None", StringComparison.OrdinalIgnoreCase) && !modifiers.Equals("Nenhum", StringComparison.OrdinalIgnoreCase))
        {
            var rawParts = modifiers.Split(new[] { '+', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in rawParts)
            {
                string norm = p.Trim();
                if (norm.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || norm.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    if (!parts.Contains("Ctrl")) parts.Add("Ctrl");
                }
                else if (norm.Equals("Win", StringComparison.OrdinalIgnoreCase) || norm.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                {
                    if (!parts.Contains("Win")) parts.Add("Win");
                }
                else if (norm.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    if (!parts.Contains("Alt")) parts.Add("Alt");
                }
                else if (norm.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    if (!parts.Contains("Shift")) parts.Add("Shift");
                }
            }
        }

        parts.Add(keyName);
        return string.Join(" + ", parts);
    }

    public static bool IsCombinationAllowed(string? modifiers, string? keyString, out string reason)
    {
        reason = string.Empty;
        string keyName = FormatKeyName(keyString);
        bool hasModifier = !string.IsNullOrWhiteSpace(modifiers) &&
                           !modifiers.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                           !modifiers.Equals("Nenhum", StringComparison.OrdinalIgnoreCase);

        // Bloqueio de combinações reservadas pelo sistema Windows
        if (modifiers != null && modifiers.Contains("Win", StringComparison.OrdinalIgnoreCase) && keyName.Equals("L", StringComparison.OrdinalIgnoreCase))
        {
            reason = "A combinação Win + L é reservada exclusivamente pelo Windows para bloqueio de tela.";
            return false;
        }

        if (modifiers != null && modifiers.Contains("Ctrl", StringComparison.OrdinalIgnoreCase) &&
            modifiers.Contains("Alt", StringComparison.OrdinalIgnoreCase) &&
            (keyName.Equals("Delete", StringComparison.OrdinalIgnoreCase) || keyName.Equals("Del", StringComparison.OrdinalIgnoreCase)))
        {
            reason = "A combinação Ctrl + Alt + Del é reservada para a segurança do Windows.";
            return false;
        }

        // Teclas de letras e dígitos precisam de modificador para não bloquear digitação
        bool isPrintScreenOrFunction = keyName.Equals("Print Screen", StringComparison.OrdinalIgnoreCase) ||
                                       (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && keyName.Length is 2 or 3 && int.TryParse(keyName[1..], out int fNum) && fNum is >= 1 and <= 12) ||
                                       keyName.Equals("Insert", StringComparison.OrdinalIgnoreCase) ||
                                       keyName.Equals("Scroll", StringComparison.OrdinalIgnoreCase) ||
                                       keyName.Equals("Pause", StringComparison.OrdinalIgnoreCase);

        if (!hasModifier && !isPrintScreenOrFunction)
        {
            reason = "Letras, números e símbolos precisam ser combinados com Ctrl, Alt, Shift ou Win para não interferir na digitação normal.";
            return false;
        }

        return true;
    }

    public (bool success, string message) TryRegister(string modifiers, string keyString, bool saveToSettings = true)
    {
        if (_source == null)
        {
            Initialize();
        }

        if (!IsCombinationAllowed(modifiers, keyString, out string validationReason))
        {
            return (false, validationReason);
        }

        // Guarda estado anterior para restauração em caso de falha
        string prevModifiers = SettingsService.Instance.Current.HotkeyModifiers;
        string prevKey = SettingsService.Instance.Current.HotkeyKey;

        Unregister();

        uint modFlags = GetModifierFlags(modifiers);
        uint vk = GetVirtualKey(keyString);

        bool ok = RegisterHotKey(_source!.Handle, HOTKEY_ID, modFlags, vk);
        if (!ok && (modFlags & MOD_NOREPEAT) != 0)
        {
            ok = RegisterHotKey(_source.Handle, HOTKEY_ID, modFlags & ~MOD_NOREPEAT, vk);
        }

        if (ok)
        {
            _isRegistered = true;
            if (saveToSettings)
            {
                SettingsService.Instance.Current.HotkeyModifiers = modifiers;
                SettingsService.Instance.Current.HotkeyKey = keyString;
                SettingsService.Instance.Save();
            }

            string display = FormatHotkeyDisplay(modifiers, keyString);
            Logger.Info($"Atalho global ativado com sucesso: {display}");
            return (true, $"Atalho {display} configurado e ativo com sucesso!");
        }
        else
        {
            int err = Marshal.GetLastWin32Error();
            string display = FormatHotkeyDisplay(modifiers, keyString);
            string errorReason = err switch
            {
                1409 => $"O atalho {display} já está em uso por outro aplicativo ou pelo próprio Windows.",
                _ => $"Não foi possível registrar {display} no Windows (Código Win32: {err})."
            };

            Logger.Warn(errorReason);

            // Restaura o atalho anterior para nunca deixar o usuário sem atalho funcional
            RegisterConfiguredHotkey();

            return (false, $"{errorReason} O atalho anterior ({FormatHotkeyDisplay(prevModifiers, prevKey)}) foi mantido ativo.");
        }
    }

    public bool RegisterConfiguredHotkey()
    {
        if (_source == null) return false;

        Unregister();

        var settings = SettingsService.Instance.Current;
        uint modifiers = GetModifierFlags(settings.HotkeyModifiers);
        uint vk = GetVirtualKey(settings.HotkeyKey);

        bool success = RegisterHotKey(_source.Handle, HOTKEY_ID, modifiers, vk);
        if (!success && (modifiers & MOD_NOREPEAT) != 0)
        {
            success = RegisterHotKey(_source.Handle, HOTKEY_ID, modifiers & ~MOD_NOREPEAT, vk);
        }

        if (success)
        {
            _isRegistered = true;
            Logger.Info($"Atalho global registrado com sucesso: {FormatHotkeyDisplay(settings.HotkeyModifiers, settings.HotkeyKey)} (VK: 0x{vk:X2})");
        }
        else
        {
            int err = Marshal.GetLastWin32Error();
            string msg = $"Não foi possível registrar o atalho global {FormatHotkeyDisplay(settings.HotkeyModifiers, settings.HotkeyKey)}. Possível conflito com outro aplicativo (Erro Win32: {err}).";
            Logger.Warn(msg);
            HotkeyRegistrationFailed?.Invoke(msg);
        }

        return success;
    }

    public (bool success, string message) ReRegisterHotkey()
    {
        try
        {
            if (_source == null)
            {
                Initialize();
            }
            else
            {
                Unregister();
            }

            var settings = SettingsService.Instance.Current;
            bool ok = RegisterConfiguredHotkey();
            string display = FormatHotkeyDisplay(settings.HotkeyModifiers, settings.HotkeyKey);
            if (ok)
            {
                return (true, $"Atalho {display} registrado e ativo com sucesso.");
            }
            else
            {
                return (false, $"Não foi possível registrar o atalho {display}. Outro aplicativo ou a Ferramenta de Captura do Windows pode estar bloqueando a tecla.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao reinicializar atalho global.", ex);
            return (false, $"Erro ao tentar reativar atalho: {ex.Message}");
        }
    }

    public void Unregister()
    {
        if (_isRegistered && _source != null)
        {
            UnregisterHotKey(_source.Handle, HOTKEY_ID);
            _isRegistered = false;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && (int)(wParam.ToInt64() & 0xFFFF) == HOTKEY_ID)
        {
            handled = true;
            try
            {
                HotkeyPressed?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao disparar manipulador do atalho global.", ex);
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        if (_source != null)
        {
            _source.RemoveHook(HwndHook);
            _source.Dispose();
            _source = null;
        }
    }
}
