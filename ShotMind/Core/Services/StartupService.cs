using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ShotMind.Core.Services;

public static class StartupService
{
    public const string AppName = "CrankoShot";
    public const string LegacyAppName = "ShotMind";
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static readonly string[] StartupShortcutNames = new[]
    {
        "CrankoShot.lnk",
        "ShotMind.lnk"
    };

    /// <summary>
    /// Verifica se a inicialização automática com o Windows está ativa.
    /// O mecanismo único e oficial é a chave em HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
    /// </summary>
    public static bool IsRunOnStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            var value = key?.GetValue(AppName);
            if (value != null)
            {
                return true;
            }

            // Verifica resquício do nome legado no registro
            var legacyVal = key?.GetValue(LegacyAppName);
            if (legacyVal != null)
            {
                return true;
            }

            // Verifica se existia atalho residual na pasta Inicializar
            string userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            foreach (var name in StartupShortcutNames)
            {
                if (File.Exists(Path.Combine(userStartup, name)))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao verificar inicialização com o Windows.", ex);
            return false;
        }
    }

    /// <summary>
    /// Ativa ou desativa a inicialização automática com o Windows de forma definitiva:
    /// - Remove SEMPRE qualquer atalho residual na pasta Startup (User e Common) para evitar duplicatas.
    /// - Remove entradas legadas com o nome ShotMind.
    /// - Registra ou remove a entrada única oficial "CrankoShot" em HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
    /// </summary>
    public static void SetRunOnStartup(bool enable)
    {
        try
        {
            // 1. Limpa SEMPRE qualquer atalho duplicado na pasta Startup do Windows
            CleanDuplicateStartupShortcuts();

            using var key = Registry.CurrentUser.CreateSubKey(RunRegistryKey, true);
            if (key == null) return;

            // Remove valor legado "ShotMind" do registro
            if (key.GetValue(LegacyAppName) != null)
            {
                try { key.DeleteValue(LegacyAppName, false); } catch { }
            }

            if (enable)
            {
                string? exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\" --background");
                    Logger.Info("CrankoShot configurado para iniciar com o Windows via Registro (Run).");
                }
            }
            else
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                    Logger.Info("CrankoShot removido da inicialização do Windows.");
                }
            }

            // Limpa entradas no StartupApproved para evitar que o Gerenciador de Tarefas exiba registros órfãos
            CleanStartupApprovedEntries();
        }
        catch (Exception ex)
        {
            Logger.Error($"Erro ao alterar inicialização com o Windows (enable={enable}).", ex);
        }
    }

    /// <summary>
    /// Sincroniza a inicialização com a preferência do aplicativo e remove duplicatas legadas.
    /// </summary>
    public static void ApplyStartupPreference(bool enable)
    {
        SetRunOnStartup(enable);
    }

    /// <summary>
    /// Remove atalhos .lnk pertencentes ao CrankoShot/ShotMind das pastas Startup do usuário e de todos os usuários,
    /// garantindo que apenas a entrada no Registro seja mantida.
    /// </summary>
    public static void CleanDuplicateStartupShortcuts()
    {
        try
        {
            var folders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
            };

            foreach (var folder in folders)
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) continue;

                foreach (var name in StartupShortcutNames)
                {
                    string path = Path.Combine(folder, name);
                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Delete(path);
                            Logger.Info($"Atalho de inicialização duplicado removido com sucesso: {path}");
                        }
                        catch (Exception exDel)
                        {
                            Logger.Warn($"Não foi possível remover atalho residual {path}: {exDel.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Aviso ao limpar atalhos duplicados de inicialização: {ex.Message}");
        }
    }

    /// <summary>
    /// Limpa entradas legadas do ShotMind nas chaves StartupApproved do Windows Explorer.
    /// </summary>
    private static void CleanStartupApprovedEntries()
    {
        try
        {
            using var runApproved = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", true);
            if (runApproved?.GetValue(LegacyAppName) != null)
            {
                try { runApproved.DeleteValue(LegacyAppName, false); } catch { }
            }

            using var folderApproved = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder", true);
            foreach (var name in StartupShortcutNames)
            {
                if (folderApproved?.GetValue(name) != null)
                {
                    try { folderApproved.DeleteValue(name, false); } catch { }
                }
            }
        }
        catch { }
    }
}
