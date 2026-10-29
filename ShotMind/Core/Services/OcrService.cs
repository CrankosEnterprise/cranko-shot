using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ShotMind.Core.Services;

public class OcrResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = string.Empty;
    public string LanguageUsed { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public static class OcrService
{
    public static bool IsOcrAvailable()
    {
        try
        {
            return CheckOcrAvailableCore();
        }
        catch
        {
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CheckOcrAvailableCore()
    {
        return OcrEngine.AvailableRecognizerLanguages.Count > 0;
    }

    public static string[] GetAvailableLanguages()
    {
        try
        {
            return GetAvailableLanguagesCore();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string[] GetAvailableLanguagesCore()
    {
        return OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToArray();
    }

    public static async Task<OcrResult> RecognizeTextAsync(Bitmap bitmap)
    {
        try
        {
            return await RecognizeTextCoreAsync(bitmap);
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao executar OCR local.", ex);
            return new OcrResult
            {
                Success = false,
                ErrorMessage = $"Falha ao processar texto com o OCR local: {ex.Message}"
            };
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<OcrResult> RecognizeTextCoreAsync(Bitmap bitmap)
    {
        try
        {
            if (!IsOcrAvailable())
            {
                return new OcrResult
                {
                    Success = false,
                    ErrorMessage = "O recurso de OCR do Windows não possui idiomas reconhecedores instalados no sistema."
                };
            }

            // Determine language: try pt-BR first, then pt, then current user language, then first available
            var prefLang = SettingsService.Instance.Current.PreferredOcrLanguage;
            var availableLangs = OcrEngine.AvailableRecognizerLanguages.ToList();

            var lang = availableLangs.FirstOrDefault(l => l.LanguageTag.Equals(prefLang, StringComparison.OrdinalIgnoreCase))
                       ?? availableLangs.FirstOrDefault(l => l.LanguageTag.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
                       ?? availableLangs.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                       ?? availableLangs.FirstOrDefault();

            OcrEngine engine;
            if (lang != null && OcrEngine.IsLanguageSupported(lang))
            {
                engine = OcrEngine.TryCreateFromLanguage(lang);
            }
            else
            {
                engine = OcrEngine.TryCreateFromUserProfileLanguages();
            }

            if (engine == null)
            {
                return new OcrResult
                {
                    Success = false,
                    ErrorMessage = "Não foi possível inicializar o mecanismo de OCR com os idiomas disponíveis."
                };
            }

            // Convert System.Drawing.Bitmap to Windows.Graphics.Imaging.SoftwareBitmap
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            using var randomAccessStream = new InMemoryRandomAccessStream();
            using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
            {
                using var dw = new DataWriter(outputStream);
                dw.WriteBytes(ms.ToArray());
                await dw.StoreAsync();
                await outputStream.FlushAsync();
            }

            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            // SoftwareBitmap needs to be in a supported pixel format (Bgra8 or Nv12)
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            var ocrEngineResult = await engine.RecognizeAsync(softwareBitmap);

            if (ocrEngineResult.Lines.Count == 0)
            {
                return new OcrResult
                {
                    Success = true,
                    Text = "Nenhum texto detectado na imagem selecionada.",
                    LanguageUsed = engine.RecognizerLanguage.LanguageTag
                };
            }

            var sb = new StringBuilder();
            foreach (var line in ocrEngineResult.Lines)
            {
                sb.AppendLine(line.Text);
            }

            string extractedText = sb.ToString().TrimEnd();

            Logger.Info($"OCR realizado com sucesso ({engine.RecognizerLanguage.LanguageTag}): {extractedText.Length} caracteres.");

            return new OcrResult
            {
                Success = true,
                Text = extractedText,
                LanguageUsed = engine.RecognizerLanguage.LanguageTag
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao executar OCR local.", ex);
            return new OcrResult
            {
                Success = false,
                ErrorMessage = $"Falha ao processar texto com o OCR local: {ex.Message}"
            };
        }
    }
}
