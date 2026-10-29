using System;
using System.IO;

namespace ShotMind.Core.Services;

public static class Logger
{
    private static readonly string LogDirectory;
    private static readonly string LogFilePath;
    private static readonly object LockObj = new();

    [ThreadStatic]
    private static bool _isLogging;

    static Logger()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        LogDirectory = Path.Combine(appData, "ShotMind", "logs");
        LogFilePath = Path.Combine(LogDirectory, "shotmind.log");

        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }
        catch
        {
            // Fallback
        }
    }

    public static string GetLogDirectory() => LogDirectory;
    public static string GetLogFilePath() => LogFilePath;

    public static void OpenLogFolder()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Error("Falha ao abrir pasta de logs.", ex);
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        string fullMessage = ex != null ? $"{message} | Detalhes: {ex.Message} \n{ex.StackTrace}" : message;
        Log("ERROR", fullMessage);
    }

    public static void LogException(string module, string operation, Exception ex, string? contextInfo = null)
    {
        string header = $"[Módulo: {module}] [Operação: {operation}] Tipo: {ex.GetType().FullName} | Mensagem: {ex.Message}";
        if (!string.IsNullOrEmpty(contextInfo))
        {
            header += $" | Contexto: {contextInfo}";
        }
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            header += $"\nStack: {ex.StackTrace}";
        }
        if (ex.InnerException != null)
        {
            header += $"\nInner Exception ({ex.InnerException.GetType().Name}): {ex.InnerException.Message}\nStack: {ex.InnerException.StackTrace}";
        }
        Log("FATAL/ERROR", header);
    }

    private static void Log(string level, string message)
    {
        if (_isLogging) return;
        _isLogging = true;

        try
        {
            // Sanitize: never log API keys or bearer tokens
            string sanitized = Sanitize(message);

            lock (LockObj)
            {
                try
                {
                    if (!Directory.Exists(LogDirectory))
                    {
                        Directory.CreateDirectory(LogDirectory);
                    }
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {sanitized}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, line);
                }
                catch
                {
                    // Silently ignore logging failures to not disrupt the app
                }
            }
        }
        finally
        {
            _isLogging = false;
        }
    }

    private static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // 1. Mask direct Google Gemini API keys (AIza...) and OpenRouter API keys (sk-or-...)
        string sanitized = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"(AIza[0-9A-Za-z\-_]{10,}|sk-or-v1-[0-9A-Za-z\-_]{10,}|sk-or-[0-9A-Za-z\-_]{10,})",
            "[CHAVE_PROTEGIDA]");

        // 2. Mask Bearer tokens and key/token/secret assignments
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"(?i)(Bearer\s+)([a-zA-Z0-9_\-\.]{8,})",
            "$1[CHAVE_PROTEGIDA]");

        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"(?i)(key|token|secret|password|api[_\s-]?key)\s*[:=]\s*([a-zA-Z0-9_\-]{8,})",
            "$1: [CHAVE_PROTEGIDA]");

        // 3. Mask active API keys if configured (without triggering lazy initialization recursion)
        try
        {
            string? activeKey = SettingsService.TryGetLoadedApiKey();
            if (!string.IsNullOrEmpty(activeKey) && sanitized.Contains(activeKey))
            {
                sanitized = sanitized.Replace(activeKey, "[CHAVE_PROTEGIDA]");
            }

            string? openRouterKey = SettingsService.TryGetLoadedOpenRouterApiKey();
            if (!string.IsNullOrEmpty(openRouterKey) && sanitized.Contains(openRouterKey))
            {
                sanitized = sanitized.Replace(openRouterKey, "[CHAVE_PROTEGIDA]");
            }
        }
        catch { }

        return sanitized;
    }
}
