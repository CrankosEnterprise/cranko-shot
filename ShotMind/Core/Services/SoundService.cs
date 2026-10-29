using System;
using System.Media;

namespace ShotMind.Core.Services;

public static class SoundService
{
    public static void PlayCaptureSound()
    {
        if (!SettingsService.Instance.Current.PlaySoundOnCapture) return;

        try
        {
            // Windows built-in click/beep sound
            SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Não foi possível reproduzir som de captura: {ex.Message}");
        }
    }
}
