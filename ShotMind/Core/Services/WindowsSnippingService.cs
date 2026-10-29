using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ShotMind.Core.Services;

public static class WindowsSnippingService
{
    private const string KeyboardRegistryPath = @"Control Panel\Keyboard";
    private const string ValueName = "PrintScreenKeyForSnippingEnabled";

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);

    private const uint HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    public static bool IsSnippingOverrideActive()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyboardRegistryPath);
            if (key != null)
            {
                object? val = key.GetValue(ValueName);
                if (val is int intVal)
                {
                    // 0 means Snipping Tool is disabled on PrtScn (ShotMind override active)
                    return intVal == 0;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao verificar configuração de Print Screen do Windows.", ex);
        }

        return false;
    }

    public static bool SetSnippingOverride(bool overrideSnipping)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyboardRegistryPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (key != null)
            {
                // 0: disable Snipping Tool on PrintScreen (allowing ShotMind)
                // 1: restore default Windows Snipping Tool
                int dwordVal = overrideSnipping ? 0 : 1;
                key.SetValue(ValueName, dwordVal, RegistryValueKind.DWord);

                BroadcastSettingChange();
                Logger.Info($"Configuração PrintScreenKeyForSnippingEnabled atualizada para {dwordVal}.");
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Falha ao atualizar registro para PrintScreenKeyForSnippingEnabled.", ex);
        }

        return false;
    }

    public static void BroadcastSettingChange()
    {
        try
        {
            SendMessageTimeout(
                (IntPtr)HWND_BROADCAST,
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                KeyboardRegistryPath,
                SMTO_ABORTIFHUNG,
                1500,
                out _);
        }
        catch (Exception ex)
        {
            Logger.Error("Falha ao emitir WM_SETTINGCHANGE.", ex);
        }
    }
}
