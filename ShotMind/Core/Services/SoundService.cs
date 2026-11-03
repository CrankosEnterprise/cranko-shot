using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;
using ShotMind.Core.Models;

namespace ShotMind.Core.Services;

/// <summary>Opção exibida na lista de sons de captura das configurações.</summary>
public sealed record SoundOption(string Id, string DisplayName, bool IsCustom);

/// <summary>
/// Reprodução do som de captura.
/// - A reprodução acontece em uma thread própria (com Dispatcher), portanto nunca atrasa nem bloqueia a captura.
/// - O volume (0-100) é o nível do próprio CrankoShot: independe de qualquer ajuste feito no volume geral do Windows.
/// - Sons importados são copiados para %AppData%\ShotMind\sounds\custom e persistem entre execuções.
/// </summary>
public static class SoundService
{
    public const string SystemSoundId = "system";
    public const string BuiltInPrefix = "builtin:";
    public const string CustomPrefix = "custom:";

    public const long MaxImportBytes = 10L * 1024 * 1024;
    public const double MaxImportSeconds = 10.0;
    private const int SampleRate = 44100;

    private static readonly string[] SupportedExtensions = { ".wav", ".mp3" };

    private static readonly (string Id, string Name)[] BuiltIns =
    {
        ("shutter", "Obturador de câmera"),
        ("soft", "Toque suave"),
        ("chime", "Sino"),
        ("pop", "Pop"),
        ("beep", "Bipe curto")
    };

    private sealed class Slot
    {
        public System.Windows.Media.MediaPlayer? Player;
        public string? Path;
    }

    private static readonly Slot CaptureSlot = new();
    private static readonly Slot PreviewSlot = new();
    private static readonly object ThreadLock = new();
    private static Dispatcher? _soundDispatcher;

    private static string SoundsRoot => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotMind", "sounds");
    public static string CustomDirectory => System.IO.Path.Combine(SoundsRoot, "custom");
    private static string BuiltInDirectory => System.IO.Path.Combine(SoundsRoot, "builtin");

    #region Thread de áudio

    private static Dispatcher? GetDispatcher()
    {
        lock (ThreadLock)
        {
            if (_soundDispatcher != null) return _soundDispatcher;

            using var ready = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                _soundDispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "CrankoShot.Sound"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait(3000);
            return _soundDispatcher;
        }
    }

    private static void EnsureLoaded(Slot slot, string path)
    {
        if (slot.Player == null)
        {
            var player = new System.Windows.Media.MediaPlayer();
            player.MediaFailed += (_, e) =>
                Logger.Warn($"Falha ao reproduzir som de captura: {e.ErrorException?.Message}");
            slot.Player = player;
        }

        if (!string.Equals(slot.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            slot.Player.Open(new Uri(path, UriKind.Absolute));
            slot.Path = path;
        }
    }

    #endregion

    #region Reprodução

    /// <summary>Pré-carrega o som configurado para que a primeira captura toque sem latência.</summary>
    public static void Warmup()
    {
        try
        {
            var s = SettingsService.Instance.Current;
            var customs = s.CustomCaptureSounds.ToArray();
            string soundId = s.CaptureSoundId;
            int volume = s.CaptureSoundVolume;

            var d = GetDispatcher();
            d?.BeginInvoke(new Action(() =>
            {
                try
                {
                    string? path = ResolveSoundPath(soundId, customs) ?? ResolveSoundPath(SystemSoundId, customs);
                    if (path == null) return;
                    EnsureLoaded(CaptureSlot, path);
                    CaptureSlot.Player!.Volume = ToPlayerVolume(volume);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Não foi possível pré-carregar o som de captura: {ex.Message}");
                }
            }));
        }
        catch (Exception ex)
        {
            Logger.Warn($"Não foi possível pré-carregar o som de captura: {ex.Message}");
        }
    }

    /// <summary>Indica se a captura deve tocar som (respeita o interruptor geral, o silenciamento e o volume).</summary>
    public static bool ShouldPlayCaptureSound(AppSettings s)
        => s.PlaySoundOnCapture && !s.CaptureSoundMuted && s.CaptureSoundVolume > 0;

    /// <summary>Converte o volume configurado (0-100) para o volume do player (0.0-1.0).</summary>
    public static double ToPlayerVolume(int volumePercent) => Math.Clamp(volumePercent, 0, 100) / 100.0;

    public static void PlayCaptureSound()
    {
        try
        {
            var s = SettingsService.Instance.Current;
            if (!ShouldPlayCaptureSound(s)) return;

            PlayOnSlot(CaptureSlot, s.CaptureSoundId, s.CaptureSoundVolume, s.CustomCaptureSounds.ToArray());
        }
        catch (Exception ex)
        {
            Logger.Warn($"Não foi possível reproduzir som de captura: {ex.Message}");
        }
    }

    /// <summary>
    /// Pré-visualização do som selecionado, respeitando o volume informado (mesmo antes de salvar).
    /// Retorna false quando o som está silenciado ou com volume zero.
    /// </summary>
    public static bool PreviewSound(string soundId, int volume, bool muted)
    {
        if (muted || volume <= 0) return false;

        try
        {
            var customs = SettingsService.Instance.Current.CustomCaptureSounds.ToArray();
            PlayOnSlot(PreviewSlot, soundId, volume, customs);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Não foi possível pré-visualizar o som: {ex.Message}");
            return false;
        }
    }

    private static void PlayOnSlot(Slot slot, string soundId, int volume, CustomCaptureSound[] customs)
    {
        var d = GetDispatcher();
        if (d == null)
        {
            SystemSounds.Asterisk.Play();
            return;
        }

        d.BeginInvoke(new Action(() =>
        {
            try
            {
                string? path = ResolveSoundPath(soundId, customs) ?? ResolveSoundPath(SystemSoundId, customs);
                if (path == null)
                {
                    // Sem arquivo de som disponível: mantém o comportamento original (volume do sistema).
                    SystemSounds.Asterisk.Play();
                    return;
                }

                EnsureLoaded(slot, path);
                slot.Player!.Volume = ToPlayerVolume(volume);
                slot.Player.Stop();
                slot.Player.Play();
            }
            catch (Exception ex)
            {
                Logger.Warn($"Não foi possível reproduzir o som: {ex.Message}");
            }
        }));
    }

    #endregion

    #region Catálogo de sons

    public static List<SoundOption> GetAvailableSounds()
    {
        var list = new List<SoundOption>
        {
            new(SystemSoundId, "Som do Windows (padrão)", false)
        };

        foreach (var (id, name) in BuiltIns)
        {
            list.Add(new SoundOption(BuiltInPrefix + id, name, false));
        }

        foreach (var c in SettingsService.Instance.Current.CustomCaptureSounds)
        {
            list.Add(new SoundOption(CustomPrefix + c.Id, $"Importado — {c.DisplayName}", true));
        }

        return list;
    }

    public static string? ResolveSoundPath(string? soundId, IReadOnlyCollection<CustomCaptureSound> customs)
    {
        if (string.IsNullOrWhiteSpace(soundId)) soundId = SystemSoundId;

        if (soundId.StartsWith(BuiltInPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return EnsureBuiltInFile(soundId[BuiltInPrefix.Length..]);
        }

        if (soundId.StartsWith(CustomPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string id = soundId[CustomPrefix.Length..];
            var custom = customs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            if (custom == null) return null;

            string path = System.IO.Path.Combine(CustomDirectory, custom.FileName);
            return File.Exists(path) ? path : null;
        }

        return GetWindowsSystemSoundPath();
    }

    private static string? GetWindowsSystemSoundPath()
    {
        try
        {
            var raw = Registry.GetValue(
                @"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\SystemAsterisk\.Current", "", null) as string;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                string path = Environment.ExpandEnvironmentVariables(raw.Trim());
                if (File.Exists(path)) return path;
            }
        }
        catch
        {
            // Registro indisponível: usa o fallback do sistema.
        }
        return null;
    }

    #endregion

    #region Importação e remoção de sons personalizados

    public static async Task<(bool Success, string Message, CustomCaptureSound? Sound)> ImportCustomSoundAsync(string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return (false, "Arquivo não encontrado.", null);
            }

            string ext = System.IO.Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext))
            {
                return (false, "Formato não suportado. Selecione um arquivo WAV ou MP3.", null);
            }

            var info = new FileInfo(sourcePath);
            if (info.Length == 0)
            {
                return (false, "O arquivo de áudio está vazio.", null);
            }
            if (info.Length > MaxImportBytes)
            {
                return (false, $"O arquivo é grande demais (máximo {MaxImportBytes / (1024 * 1024)} MB). Use um som curto de captura.", null);
            }

            Directory.CreateDirectory(CustomDirectory);
            string id = Guid.NewGuid().ToString("N");
            string fileName = id + ext;
            string dest = System.IO.Path.Combine(CustomDirectory, fileName);
            await Task.Run(() => File.Copy(sourcePath, dest, overwrite: false));

            var (ok, error) = await ValidateAudioFileAsync(dest);
            if (!ok)
            {
                try { File.Delete(dest); } catch { }
                return (false, error, null);
            }

            string displayName = System.IO.Path.GetFileNameWithoutExtension(sourcePath).Trim();
            if (displayName.Length == 0) displayName = "Som personalizado";
            if (displayName.Length > 40) displayName = displayName[..40];

            var sound = new CustomCaptureSound { Id = id, DisplayName = displayName, FileName = fileName };
            SettingsService.Instance.Current.CustomCaptureSounds.Add(sound);
            SettingsService.Instance.Save();

            return (true, $"Som \"{displayName}\" importado com sucesso.", sound);
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao importar som de captura.", ex);
            return (false, $"Não foi possível importar o arquivo: {ex.Message}", null);
        }
    }

    public static bool RemoveCustomSound(string id)
    {
        var settings = SettingsService.Instance.Current;
        var sound = settings.CustomCaptureSounds.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (sound == null) return false;

        string path = System.IO.Path.Combine(CustomDirectory, sound.FileName);
        ReleaseSlotsUsing(path);

        settings.CustomCaptureSounds.Remove(sound);
        if (string.Equals(settings.CaptureSoundId, CustomPrefix + sound.Id, StringComparison.OrdinalIgnoreCase))
        {
            settings.CaptureSoundId = SystemSoundId;
        }
        SettingsService.Instance.Save();

        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Som removido da lista, mas o arquivo não pôde ser apagado: {ex.Message}");
        }

        return true;
    }

    private static void ReleaseSlotsUsing(string path)
    {
        var d = _soundDispatcher;
        if (d == null) return;

        try
        {
            d.Invoke(new Action(() =>
            {
                foreach (var slot in new[] { CaptureSlot, PreviewSlot })
                {
                    if (slot.Path != null && string.Equals(slot.Path, path, StringComparison.OrdinalIgnoreCase))
                    {
                        slot.Player?.Close();
                        slot.Path = null;
                    }
                }
            }), DispatcherPriority.Send, CancellationToken.None, TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            Logger.Warn($"Não foi possível liberar o arquivo de som: {ex.Message}");
        }
    }

    /// <summary>Confirma que o Windows consegue abrir o áudio e que ele é curto o bastante para uma captura.</summary>
    private static Task<(bool Ok, string Error)> ValidateAudioFileAsync(string path)
    {
        var d = GetDispatcher();
        if (d == null) return Task.FromResult((true, string.Empty));

        var tcs = new TaskCompletionSource<(bool, string)>(TaskCreationOptions.RunContinuationsAsynchronously);

        d.BeginInvoke(new Action(() =>
        {
            var player = new System.Windows.Media.MediaPlayer { Volume = 0 };
            DispatcherTimer? timer = null;
            bool finished = false;

            void Finish(bool ok, string message)
            {
                if (finished) return;
                finished = true;
                timer?.Stop();
                try { player.Close(); } catch { }
                tcs.TrySetResult((ok, message));
            }

            player.MediaOpened += (_, _) =>
            {
                if (player.NaturalDuration.HasTimeSpan && player.NaturalDuration.TimeSpan.TotalSeconds > MaxImportSeconds)
                {
                    Finish(false, $"O áudio tem mais de {MaxImportSeconds:0} segundos. Escolha um som curto para a captura.");
                }
                else
                {
                    Finish(true, string.Empty);
                }
            };
            player.MediaFailed += (_, _) =>
                Finish(false, "O Windows não conseguiu abrir este áudio (arquivo corrompido ou codec não suportado).");

            timer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Normal,
                (_, _) => Finish(false, "Tempo esgotado ao ler o arquivo de áudio."), d);

            try
            {
                player.Open(new Uri(path, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                Finish(false, $"Não foi possível abrir o áudio: {ex.Message}");
            }
        }));

        return tcs.Task;
    }

    #endregion

    #region Sons padrão sintetizados

    private static string? EnsureBuiltInFile(string key)
    {
        try
        {
            Directory.CreateDirectory(BuiltInDirectory);
            string path = System.IO.Path.Combine(BuiltInDirectory, $"{key}_v1.wav");
            if (!File.Exists(path) || new FileInfo(path).Length < 100)
            {
                byte[]? wav = SynthesizeBuiltIn(key);
                if (wav == null) return null;
                File.WriteAllBytes(path, wav);
            }
            return path;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Não foi possível preparar o som padrão '{key}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Gera o WAV (PCM 16 bits, mono, 44,1 kHz) de um dos sons padrão. Retorna null se a chave for desconhecida.</summary>
    public static byte[]? SynthesizeBuiltIn(string key)
    {
        double twoPi = 2 * Math.PI;

        switch (key)
        {
            case "shutter":
            {
                uint seed = 0x1234ABCD;
                double lowPass = 0;
                return ToWav(Render(0.2, t =>
                {
                    seed = unchecked(seed * 1664525u + 1013904223u);
                    double noise = ((seed >> 8) / (double)(1 << 24)) * 2.0 - 1.0;
                    lowPass += 0.35 * (noise - lowPass);

                    double first = t < 0.05 ? Math.Exp(-t / 0.010) : 0;
                    double t2 = t - 0.075;
                    double second = t2 >= 0 && t2 < 0.09 ? 0.7 * Math.Exp(-t2 / 0.016) : 0;
                    double click = Math.Sin(twoPi * 2200 * t) * Math.Exp(-t / 0.004) * 0.25;

                    return lowPass * 3.0 * (first + second) * 0.6 + click;
                }));
            }
            case "soft":
                return ToWav(Render(0.3, t =>
                {
                    double env = Math.Min(1, t / 0.004) * Math.Exp(-t / 0.08);
                    return (Math.Sin(twoPi * 880 * t) + 0.3 * Math.Sin(twoPi * 1320 * t)) * 0.55 * env;
                }));
            case "chime":
                return ToWav(Render(0.8, t =>
                {
                    double env = Math.Min(1, t / 0.003) * Math.Exp(-t / 0.22);
                    return (Math.Sin(twoPi * 1046.5 * t) + 0.4 * Math.Sin(twoPi * 2093 * t) + 0.15 * Math.Sin(twoPi * 3136 * t)) * 0.5 * env;
                }));
            case "pop":
            {
                double phase = 0;
                return ToWav(Render(0.14, t =>
                {
                    double freq = 700 * Math.Exp(-t / 0.03) + 200;
                    phase += twoPi * freq / SampleRate;
                    return Math.Sin(phase) * Math.Exp(-t / 0.035) * 0.85;
                }));
            }
            case "beep":
                return ToWav(Render(0.09, t =>
                {
                    double env = Math.Min(1, t / 0.004) * Math.Min(1, (0.09 - t) / 0.01);
                    return Math.Sin(twoPi * 1200 * t) * 0.5 * env;
                }));
            default:
                return null;
        }
    }

    private static short[] Render(double seconds, Func<double, double> generator)
    {
        int count = (int)(seconds * SampleRate);
        int fade = (int)(0.003 * SampleRate);
        var data = new short[count];

        for (int i = 0; i < count; i++)
        {
            double v = Math.Clamp(generator(i / (double)SampleRate), -1.0, 1.0);
            if (i > count - fade) v *= (count - i) / (double)fade;
            data[i] = (short)Math.Round(v * short.MaxValue * 0.9);
        }

        return data;
    }

    private static byte[] ToWav(short[] samples)
    {
        int dataLength = samples.Length * 2;
        using var ms = new MemoryStream(44 + dataLength);
        using var bw = new BinaryWriter(ms);

        bw.Write("RIFF"u8);
        bw.Write(36 + dataLength);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1);          // PCM
        bw.Write((short)1);          // mono
        bw.Write(SampleRate);
        bw.Write(SampleRate * 2);    // byte rate
        bw.Write((short)2);          // block align
        bw.Write((short)16);         // bits por amostra
        bw.Write("data"u8);
        bw.Write(dataLength);
        foreach (short s in samples) bw.Write(s);

        bw.Flush();
        return ms.ToArray();
    }

    #endregion
}
