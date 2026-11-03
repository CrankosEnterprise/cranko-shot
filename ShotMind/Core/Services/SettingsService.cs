using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShotMind.Core.Models;

namespace ShotMind.Core.Services;

public class SettingsService
{
    private static readonly string SettingsDirectory;
    private static readonly string SettingsFilePath;
    private static readonly string SettingsBackupFilePath;
    private static readonly string SettingsTempFilePath;
    private static readonly object _fileLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static SettingsService? _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();

    public AppSettings Current { get; private set; } = new();

    static SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        SettingsDirectory = Path.Combine(appData, "ShotMind");
        SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");
        SettingsBackupFilePath = Path.Combine(SettingsDirectory, "settings.json.bak");
        SettingsTempFilePath = Path.Combine(SettingsDirectory, "settings.json.tmp");

        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }
        }
        catch { }
    }

    private SettingsService()
    {
        _instance = this;
        ValidateSettings(Current);
        Current = Load();
    }

    public static void ValidateSettings(AppSettings s)
    {
        if (s == null) return;
        if (string.IsNullOrWhiteSpace(s.HotkeyKey)) s.HotkeyKey = "Snapshot";
        if (string.IsNullOrWhiteSpace(s.HotkeyModifiers)) s.HotkeyModifiers = "None";
        if (string.IsNullOrWhiteSpace(s.AccentTheme)) s.AccentTheme = "Purple";
        if (string.Equals(s.AccentTheme, "White", StringComparison.OrdinalIgnoreCase))
        {
            s.AccentTheme = "Gray";
        }
        string defaultPicturesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShotMind");
        if (string.IsNullOrWhiteSpace(s.DefaultSaveDirectory))
        {
            s.DefaultSaveDirectory = defaultPicturesDir;
        }
        try
        {
            if (!Directory.Exists(s.DefaultSaveDirectory))
            {
                Directory.CreateDirectory(s.DefaultSaveDirectory);
            }
        }
        catch
        {
            s.DefaultSaveDirectory = defaultPicturesDir;
            try
            {
                if (!Directory.Exists(s.DefaultSaveDirectory))
                {
                    Directory.CreateDirectory(s.DefaultSaveDirectory);
                }
            }
            catch { }
        }
        if (string.IsNullOrWhiteSpace(s.FilenamePattern)) s.FilenamePattern = "ShotMind_{yyyy-MM-dd_HH-mm-ss}";
        s.JpgQuality = Math.Clamp(s.JpgQuality, 10, 100);
        s.SequenceDigits = Math.Clamp(s.SequenceDigits, 1, 6);
        if (s.SequenceCounter < 0) s.SequenceCounter = 0;
        s.ObfuscationIntensity = Math.Clamp(s.ObfuscationIntensity, 2, 64);
        if (s.CursorSize is not (CrosshairService.CursorSizeSmall or CrosshairService.CursorSizeNormal or CrosshairService.CursorSizeLarge or CrosshairService.CursorSizeCustom))
        {
            s.CursorSize = CrosshairService.CursorSizeNormal;
        }
        if (s.CrosshairScaleVersion < 2)
        {
            // Migração: cada escala é um conjunto completo e independente de 4 dimensões
            if (s.CursorSize == CrosshairService.CursorSizeSmall)
            {
                s.CrosshairThickness = CrosshairService.PresetSmall.Thickness;
                s.CrosshairLength = CrosshairService.PresetSmall.Length;
                s.CrosshairGap = CrosshairService.PresetSmall.Gap;
                s.CrosshairDotSize = CrosshairService.PresetSmall.DotSize;
            }
            else if (s.CursorSize == CrosshairService.CursorSizeLarge)
            {
                s.CrosshairThickness = CrosshairService.PresetLarge.Thickness;
                s.CrosshairLength = CrosshairService.PresetLarge.Length;
                s.CrosshairGap = CrosshairService.PresetLarge.Gap;
                s.CrosshairDotSize = CrosshairService.PresetLarge.DotSize;
            }
            else if (s.CursorSize == CrosshairService.CursorSizeNormal)
            {
                s.CrosshairThickness = CrosshairService.PresetNormal.Thickness;
                s.CrosshairLength = CrosshairService.PresetNormal.Length;
                s.CrosshairGap = CrosshairService.PresetNormal.Gap;
                s.CrosshairDotSize = CrosshairService.PresetNormal.DotSize;
            }
            s.CrosshairScaleVersion = 2;
        }
        s.CaptureSoundVolume = Math.Clamp(s.CaptureSoundVolume, 0, 100);
        if (string.IsNullOrWhiteSpace(s.CaptureSoundId)) s.CaptureSoundId = "system";
        s.CustomCaptureSounds ??= new();
        if (!Enum.IsDefined(typeof(CrosshairStyle), s.CrosshairStyle)) s.CrosshairStyle = CrosshairStyle.Simple;
        s.CrosshairThickness = Math.Clamp(s.CrosshairThickness <= 0 ? 2 : s.CrosshairThickness, 1, 6);
        s.CrosshairLength = Math.Clamp(s.CrosshairLength <= 0 ? 12 : s.CrosshairLength, 4, 28);
        s.CrosshairGap = Math.Clamp(s.CrosshairGap < 0 ? 4 : s.CrosshairGap, 0, 14);
        s.CrosshairDotSize = Math.Clamp(s.CrosshairDotSize <= 0 ? 3 : s.CrosshairDotSize, 1, 10);
        if (string.IsNullOrWhiteSpace(s.CursorColor)) s.CursorColor = "#8B5CF6";
        if (!Enum.IsDefined(typeof(AiProviderType), s.AiProvider)) s.AiProvider = AiProviderType.Gemini;
        if (string.IsNullOrWhiteSpace(s.GeminiModel)) s.GeminiModel = "gemini-3.5-flash";
        if (string.IsNullOrWhiteSpace(s.OpenRouterModel)) s.OpenRouterModel = "google/gemma-4-31b-it:free";
    }

    public AppSettings Load()
    {
        lock (_fileLock)
        {
            // 1. Tenta carregar o arquivo principal
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var fileInfo = new FileInfo(SettingsFilePath);
                    if (fileInfo.Length > 5)
                    {
                        string json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
                        var settings = JsonSerializer.Deserialize<AppSettings>(json);
                        if (settings != null)
                        {
                            ValidateSettings(settings);
                            Current = settings;
                            return settings;
                        }
                    }
                    else
                    {
                        Logger.Warn("Arquivo settings.json vazio ou truncado. Tentando recuperar do backup...");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Falha ao desserializar settings.json. Tentando arquivo de backup.", ex);
                }
            }

            // 2. Tenta recuperar do backup (.bak) se o arquivo principal falhar
            if (File.Exists(SettingsBackupFilePath))
            {
                try
                {
                    var bakInfo = new FileInfo(SettingsBackupFilePath);
                    if (bakInfo.Length > 5)
                    {
                        string bakJson = File.ReadAllText(SettingsBackupFilePath, Encoding.UTF8);
                        var bakSettings = JsonSerializer.Deserialize<AppSettings>(bakJson);
                        if (bakSettings != null)
                        {
                            ValidateSettings(bakSettings);
                            Current = bakSettings;
                            Logger.Info("Configurações recuperadas com sucesso a partir de settings.json.bak.");
                            Save(bakSettings);
                            return bakSettings;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Falha ao recuperar configurações a partir do backup.", ex);
                }
            }

            // 3. Fallback: cria novas configurações padrão válidas
            var defaultSettings = new AppSettings();
            ValidateSettings(defaultSettings);
            Current = defaultSettings;
            Save(defaultSettings);
            Logger.Warn("Nenhum arquivo de configurações válido encontrado. Inicializando preferências padrão.");
            return Current;
        }
    }

    public bool Save(AppSettings? settings = null)
    {
        lock (_fileLock)
        {
            if (settings != null)
            {
                Current = settings;
            }

            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                ValidateSettings(Current);

                string json = JsonSerializer.Serialize(Current, JsonOptions);

                // Gravação atômica em arquivo temporário com flush forçado
                using (var fs = new FileStream(SettingsTempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.Write(json);
                    sw.Flush();
                    fs.Flush(true);
                }

                // Cria backup da versão funcional anterior
                if (File.Exists(SettingsFilePath))
                {
                    try
                    {
                        File.Copy(SettingsFilePath, SettingsBackupFilePath, overwrite: true);
                    }
                    catch (Exception exBak)
                    {
                        Logger.Warn($"Aviso ao atualizar backup settings.json.bak: {exBak.Message}");
                    }
                }

                // Substituição atômica segura
                File.Move(SettingsTempFilePath, SettingsFilePath, overwrite: true);
                Logger.Info("Configurações salvas atomicamente com sucesso.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Erro crítico ao salvar configurações.", ex);
                try
                {
                    if (File.Exists(SettingsTempFilePath))
                    {
                        File.Delete(SettingsTempFilePath);
                    }
                }
                catch { }

                return false;
            }
        }
    }

    public void SetApiKey(string plainApiKey)
    {
        if (string.IsNullOrWhiteSpace(plainApiKey))
        {
            Current.EncryptedGeminiApiKey = string.Empty;
        }
        else
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(plainApiKey.Trim());
                byte[] encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                Current.EncryptedGeminiApiKey = Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao criptografar chave da API do Gemini.", ex);
                throw new InvalidOperationException("Não foi possível proteger a chave com DPAPI.", ex);
            }
        }
        Save();
    }

    public string GetApiKey()
    {
        if (string.IsNullOrWhiteSpace(Current.EncryptedGeminiApiKey))
        {
            return string.Empty;
        }

        try
        {
            byte[] encrypted = Convert.FromBase64String(Current.EncryptedGeminiApiKey);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao descriptografar chave da API do Gemini.", ex);
            return string.Empty;
        }
    }

    public bool HasApiKey()
    {
        return !string.IsNullOrWhiteSpace(Current.EncryptedGeminiApiKey);
    }

    public static string? TryGetLoadedApiKey()
    {
        var inst = _instance;
        var encryptedStr = inst?.Current?.EncryptedGeminiApiKey;
        if (string.IsNullOrWhiteSpace(encryptedStr))
        {
            return null;
        }

        try
        {
            byte[] encrypted = Convert.FromBase64String(encryptedStr);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }

    public string GetMaskedApiKey()
    {
        string key = GetApiKey();
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (key.Length <= 8) return "********";
        return $"{key[..4]}...{key[^4..]}";
    }

    public void SetOpenRouterApiKey(string plainApiKey)
    {
        if (string.IsNullOrWhiteSpace(plainApiKey))
        {
            Current.EncryptedOpenRouterApiKey = string.Empty;
        }
        else
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(plainApiKey.Trim());
                byte[] encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                Current.EncryptedOpenRouterApiKey = Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao criptografar chave da API do OpenRouter.", ex);
                throw new InvalidOperationException("Não foi possível proteger a chave com DPAPI.", ex);
            }
        }
        Save();
    }

    public string GetOpenRouterApiKey()
    {
        if (string.IsNullOrWhiteSpace(Current.EncryptedOpenRouterApiKey))
        {
            return string.Empty;
        }

        try
        {
            byte[] encrypted = Convert.FromBase64String(Current.EncryptedOpenRouterApiKey);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao descriptografar chave da API do OpenRouter.", ex);
            return string.Empty;
        }
    }

    public bool HasOpenRouterApiKey()
    {
        return !string.IsNullOrWhiteSpace(Current.EncryptedOpenRouterApiKey);
    }

    public static string? TryGetLoadedOpenRouterApiKey()
    {
        var inst = _instance;
        var encryptedStr = inst?.Current?.EncryptedOpenRouterApiKey;
        if (string.IsNullOrWhiteSpace(encryptedStr))
        {
            return null;
        }

        try
        {
            byte[] encrypted = Convert.FromBase64String(encryptedStr);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }

    public string GetMaskedOpenRouterApiKey()
    {
        string key = GetOpenRouterApiKey();
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (key.Length <= 10) return "********";
        return $"{key[..6]}...{key[^4..]}";
    }
}
