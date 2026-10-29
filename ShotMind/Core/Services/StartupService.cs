using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ShotMind.Core.Services;

public static class StartupService
{
    private const string AppName = "ShotMind";
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsRunOnStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            var value = key?.GetValue(AppName);
            return value != null;
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao verificar inicialização com o Windows.", ex);
            return false;
        }
    }

    public static void SetRunOnStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunRegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                string? exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\" --background");
                    Logger.Info("ShotMind configurado para iniciar com o Windows.");
                }
            }
            else
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                    Logger.Info("ShotMind removido da inicialização do Windows.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Erro ao alterar inicialização com o Windows (enable={enable}).", ex);
        }
    }
}
