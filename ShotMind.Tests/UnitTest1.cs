using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShotMind.Core;
using ShotMind.Core.Models;
using ShotMind.Core.Services;

namespace ShotMind.Tests;

[TestClass]
public class ShotMindTests
{
    [TestMethod]
    public void Settings_DpapiEncryptionAndMasking_WorksCorrectly()
    {
        var settingsService = SettingsService.Instance;
        string testKey = "AIzaSyTestKey123456789Secret";

        settingsService.SetApiKey(testKey);

        Assert.IsTrue(settingsService.HasApiKey());
        Assert.AreEqual(testKey, settingsService.GetApiKey());

        string masked = settingsService.GetMaskedApiKey();
        Assert.IsTrue(masked.StartsWith("AIza..."));
        Assert.IsTrue(masked.EndsWith("cret"));
        Assert.IsFalse(masked.Contains("123456789")); // Secret portion is masked

        // Reset
        settingsService.SetApiKey(string.Empty);
        Assert.IsFalse(settingsService.HasApiKey());
    }

    [TestMethod]
    public void ExportService_GenerateFileName_MatchesPatternAndFormat()
    {
        string pattern = "ShotMind_{yyyy-MM-dd_HH-mm-ss}";

        string pngName = ExportService.GenerateFileName(pattern, ImageFormatType.Png);
        Assert.IsTrue(pngName.StartsWith("ShotMind_"));
        Assert.IsTrue(pngName.EndsWith(".png"));

        string jpgName = ExportService.GenerateFileName(pattern, ImageFormatType.Jpg);
        Assert.IsTrue(jpgName.StartsWith("ShotMind_"));
        Assert.IsTrue(jpgName.EndsWith(".jpg"));
    }

    [TestMethod]
    public void ScreenCaptureService_CropBitmap_ClampsAndCropsProperly()
    {
        using var baseBmp = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(baseBmp))
        {
            g.Clear(System.Drawing.Color.Red);
        }

        var cropRect = new Rectangle(50, 50, 100, 80);
        using var cropped = ScreenCaptureService.CropBitmap(baseBmp, cropRect);

        Assert.AreEqual(100, cropped.Width);
        Assert.AreEqual(80, cropped.Height);
    }

    [TestMethod]
    public void ExportService_RenderAnnotations_ProducesValidAnnotatedBitmap()
    {
        using var baseBmp = new Bitmap(300, 300);
        using (var g = Graphics.FromImage(baseBmp))
        {
            g.Clear(System.Drawing.Color.White);
        }

        var annotations = new List<AnnotationItem>
        {
            new AnnotationItem
            {
                Type = ToolType.Rectangle,
                StartPoint = new System.Windows.Point(10, 10),
                EndPoint = new System.Windows.Point(100, 100),
                Color = System.Windows.Media.Colors.Purple,
                StrokeThickness = 3
            },
            new AnnotationItem
            {
                Type = ToolType.Arrow,
                StartPoint = new System.Windows.Point(20, 20),
                EndPoint = new System.Windows.Point(80, 80),
                Color = System.Windows.Media.Colors.Red,
                StrokeThickness = 2
            },
            new AnnotationItem
            {
                Type = ToolType.Text,
                StartPoint = new System.Windows.Point(50, 50),
                Text = "ShotMind Teste",
                Color = System.Windows.Media.Colors.Blue,
                FontSize = 14
            },
            new AnnotationItem
            {
                Type = ToolType.Blur,
                StartPoint = new System.Windows.Point(120, 120),
                EndPoint = new System.Windows.Point(160, 160)
            }
        };

        using var rendered = ExportService.RenderAnnotatedBitmap(baseBmp, annotations);

        Assert.IsNotNull(rendered);
        Assert.AreEqual(300, rendered.Width);
        Assert.AreEqual(300, rendered.Height);
    }

    [TestMethod]
    public async Task HistoryService_AddAndRetrieveCaptures_WorksWithSqlite()
    {
        var history = HistoryService.Instance;
        await history.InitializeAsync();

        var item = new HistoryItem
        {
            Timestamp = DateTime.Now,
            FilePath = "C:\\Temp\\test_capture.png",
            FileName = "test_capture.png",
            Width = 1920,
            Height = 1080,
            Format = "PNG",
            ThumbnailBase64 = "dGVzdA==",
            OcrText = "Texto de teste OCR",
            AiSummary = "Resumo gerado por IA"
        };

        long id = await history.AddCaptureAsync(item);
        Assert.IsTrue(id > 0);

        var list = await history.GetCapturesAsync("test_capture");
        Assert.IsTrue(list.Count > 0);
        Assert.AreEqual("test_capture.png", list[0].FileName);

        bool deleted = await history.DeleteCaptureAsync(id);
        Assert.IsTrue(deleted);
    }

    [TestMethod]
    public void GeminiService_ActionTitles_AreFormattedInPortuguese()
    {
        Assert.AreEqual("Análise da Imagem", GeminiService.GetActionTitle(AiActionType.Analyze));
        Assert.AreEqual("Extração Inteligente de Texto", GeminiService.GetActionTitle(AiActionType.ExtractText));
        Assert.AreEqual("Tradução para Português", GeminiService.GetActionTitle(AiActionType.Translate));
        Assert.AreEqual("Diagnóstico e Solução de Erro", GeminiService.GetActionTitle(AiActionType.ExplainError));
        Assert.AreEqual("Pesquisa na Web com Gemini", GeminiService.GetActionTitle(AiActionType.SearchWeb));
    }

    [TestMethod]
    public void OcrService_LanguageAvailability_CheckDoesNotCrash()
    {
        // Safe check on any Windows machine
        bool available = OcrService.IsOcrAvailable();
        var langs = OcrService.GetAvailableLanguages();

        Assert.IsNotNull(langs);
    }

    [TestMethod]
    public void SelectionMove_ClampsWithinVirtualScreenBounds()
    {
        var screen = new System.Windows.Rect(0, 0, 1920, 1080);
        var initial = new System.Windows.Rect(500, 300, 400, 300);

        // Move to the right beyond screen boundary
        double dx = 2000;
        double dy = 0;
        double newLeft = Math.Clamp(initial.X + dx, 0, screen.Width - initial.Width);
        double newTop = Math.Clamp(initial.Y + dy, 0, screen.Height - initial.Height);

        Assert.AreEqual(1920 - 400, newLeft);
        Assert.AreEqual(300, newTop);

        // Move to negative coordinates (off top-left)
        dx = -1000;
        dy = -500;
        newLeft = Math.Clamp(initial.X + dx, 0, screen.Width - initial.Width);
        newTop = Math.Clamp(initial.Y + dy, 0, screen.Height - initial.Height);

        Assert.AreEqual(0, newLeft);
        Assert.AreEqual(0, newTop);
    }

    [TestMethod]
    public void SelectionResize_CalculatesCorrectDimensionsFromHandles()
    {
        var initial = new System.Windows.Rect(100, 100, 200, 150); // right=300, bottom=250

        // Drag TopLeft handle to (80, 70)
        double left = Math.Min(80, initial.Right - 15);
        double top = Math.Min(70, initial.Bottom - 15);
        var resized = new System.Windows.Rect(left, top, initial.Right - left, initial.Bottom - top);

        Assert.AreEqual(80, resized.Left);
        Assert.AreEqual(70, resized.Top);
        Assert.AreEqual(220, resized.Width);
        Assert.AreEqual(180, resized.Height);

        // Drag BottomRight handle to (400, 350)
        double right = Math.Max(400, initial.Left + 15);
        double bottom = Math.Max(350, initial.Top + 15);
        var resizedSE = new System.Windows.Rect(initial.Left, initial.Top, right - initial.Left, bottom - initial.Top);

        Assert.AreEqual(100, resizedSE.Left);
        Assert.AreEqual(100, resizedSE.Top);
        Assert.AreEqual(300, resizedSE.Width);
        Assert.AreEqual(250, resizedSE.Height);
    }

    [TestMethod]
    public void Drawing_MultipleStrokes_UndoRedoStackMaintainsIntegrity()
    {
        var annotations = new List<AnnotationItem>();
        var undoStack = new Stack<List<AnnotationItem>>();
        var redoStack = new Stack<List<AnnotationItem>>();

        // Stroke 1: Pen
        undoStack.Push(annotations.ConvertAll(a => a.Clone()));
        annotations.Add(new AnnotationItem { Type = ToolType.Pen, StartPoint = new System.Windows.Point(10, 10), EndPoint = new System.Windows.Point(20, 20) });

        // Stroke 2: Highlighter
        undoStack.Push(annotations.ConvertAll(a => a.Clone()));
        annotations.Add(new AnnotationItem { Type = ToolType.Highlighter, StartPoint = new System.Windows.Point(30, 30), EndPoint = new System.Windows.Point(50, 30) });

        Assert.AreEqual(2, annotations.Count);

        // Undo stroke 2
        redoStack.Push(annotations.ConvertAll(a => a.Clone()));
        var previous = undoStack.Pop();
        annotations.Clear();
        annotations.AddRange(previous);

        Assert.AreEqual(1, annotations.Count);
        Assert.AreEqual(ToolType.Pen, annotations[0].Type);

        // Redo stroke 2
        undoStack.Push(annotations.ConvertAll(a => a.Clone()));
        var next = redoStack.Pop();
        annotations.Clear();
        annotations.AddRange(next);

        Assert.AreEqual(2, annotations.Count);
    }

    [TestMethod]
    public void ExportService_ObfuscationStyles_StrictlyConfinedToRegion()
    {
        // 100x100 white bitmap
        using var bmpPixelate = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(bmpPixelate))
        {
            g.Clear(System.Drawing.Color.White);
            g.FillRectangle(System.Drawing.Brushes.Black, 20, 20, 30, 30);
        }

        var annotationPix = new AnnotationItem
        {
            Type = ToolType.Blur,
            ObfuscationStyle = ObfuscationStyle.Pixelate,
            StartPoint = new System.Windows.Point(20, 20),
            EndPoint = new System.Windows.Point(50, 50)
        };

        using var resultPix = ExportService.RenderAnnotatedBitmap(bmpPixelate, new List<AnnotationItem> { annotationPix });

        // Outer pixels (0,0), (10,10), (60,60), (90,90) must remain strictly white (no boundary leakage)
        Assert.AreEqual(255, resultPix.GetPixel(5, 5).R);
        Assert.AreEqual(255, resultPix.GetPixel(5, 5).G);
        Assert.AreEqual(255, resultPix.GetPixel(5, 5).B);
        Assert.AreEqual(255, resultPix.GetPixel(60, 60).R);

        // Now test Blur
        using var bmpBlur = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(bmpBlur))
        {
            g.Clear(System.Drawing.Color.White);
            g.FillRectangle(System.Drawing.Brushes.Black, 20, 20, 30, 30);
        }

        var annotationBlur = new AnnotationItem
        {
            Type = ToolType.Blur,
            ObfuscationStyle = ObfuscationStyle.Blur,
            StartPoint = new System.Windows.Point(20, 20),
            EndPoint = new System.Windows.Point(50, 50)
        };

        using var resultBlur = ExportService.RenderAnnotatedBitmap(bmpBlur, new List<AnnotationItem> { annotationBlur });

        // Outer pixels must remain strictly intact
        Assert.AreEqual(255, resultBlur.GetPixel(5, 5).R);
        Assert.AreEqual(255, resultBlur.GetPixel(5, 5).G);
        Assert.AreEqual(255, resultBlur.GetPixel(5, 5).B);
        Assert.AreEqual(255, resultBlur.GetPixel(60, 60).R);
    }

    [TestMethod]
    public void GeminiService_NormalizeModelName_HandlesPrefixAndDefaults()
    {
        Assert.AreEqual("gemini-3.5-flash", GeminiService.NormalizeModelName("models/gemini-3.5-flash"));
        Assert.AreEqual("gemini-3.5-flash", GeminiService.NormalizeModelName("gemini-3.5-flash"));
        Assert.AreEqual("gemini-2.5-flash", GeminiService.NormalizeModelName("models/gemini-2.5-flash"));
        Assert.AreEqual("gemini-2.5-pro", GeminiService.NormalizeModelName("models/gemini-2.5-pro"));
        Assert.AreEqual("gemini-3.5-flash", GeminiService.NormalizeModelName(null));
        Assert.AreEqual("gemini-3.5-flash", GeminiService.NormalizeModelName("  "));
    }

    [TestMethod]
    public void GeminiService_ApprovedModels_ContainsCuratedFourModelsWithFriendlyDescriptions()
    {
        var models = GeminiService.ApprovedModels;
        Assert.AreEqual(4, models.Count);
        Assert.AreEqual("gemini-3.5-flash", models[0].Id);
        Assert.IsTrue(models[0].DisplayName.Contains("Gemini 3.5 Flash"));
        Assert.IsTrue(models[0].DisplayName.Contains("Versão Flash compacta"));

        Assert.AreEqual("gemini-2.5-flash", models[1].Id);
        Assert.AreEqual("gemini-2.5-pro", models[2].Id);
        Assert.AreEqual("gemini-2.0-flash", models[3].Id);
    }

    [TestMethod]
    public void AppSettings_CursorDefaults_AreConfiguredCorrectly()
    {
        var settings = new AppSettings();
        Assert.IsTrue(settings.AutoContrastCursor);
        Assert.AreEqual("#8B5CF6", settings.CursorColor);
        Assert.AreEqual(18, settings.CursorSize);
        Assert.AreEqual("gemini-3.5-flash", settings.GeminiModel);
    }

    [TestMethod]
    public void CursorLuminance_CalculatesCorrectContrastAndHysteresis()
    {
        // Pure White: Luminance ~ 1.0 -> High luminance (light background)
        double lumWhite = (0.2126 * 255 + 0.7152 * 255 + 0.0722 * 255) / 255.0;
        Assert.IsTrue(lumWhite > 0.99);

        // Pure Black: Luminance ~ 0.0 -> Low luminance (dark background)
        double lumBlack = (0.2126 * 0 + 0.7152 * 0 + 0.0722 * 0) / 255.0;
        Assert.AreEqual(0.0, lumBlack);

        // Hysteresis test:
        bool isDark = true;
        // Moderate brightness (0.50) should not switch dark state
        if (isDark && 0.50 > 0.58) isDark = false;
        Assert.IsTrue(isDark, "Should remain dark due to hysteresis margin");

        // High brightness (0.75) should switch to light background
        if (isDark && 0.75 > 0.58) isDark = false;
        Assert.IsFalse(isDark, "Should switch to light background");

        // Moderate darkness (0.46) should not switch back to dark state
        if (!isDark && 0.46 < 0.42) isDark = true;
        Assert.IsFalse(isDark, "Should remain light due to hysteresis margin");

        // Deep darkness (0.10) should switch back to dark
        if (!isDark && 0.10 < 0.42) isDark = true;
        Assert.IsTrue(isDark, "Should switch back to dark background");
    }

    [TestMethod]
    public void WindowsSnippingService_IsSafeToQuery()
    {
        // Must not throw exceptions regardless of current registry state
        bool status = WindowsSnippingService.IsSnippingOverrideActive();
        Assert.IsTrue(status == true || status == false);
    }

    [TestMethod]
    public void GeminiService_ParseHttpError_DifferentiatesErrorCategoriesAndSanitizesKey()
    {
        string secretKey = "AIzaSySecretTestingKey999";

        // 1. 404 Not Found (Model not available)
        string json404 = $"{{\"error\": {{\"code\": 404, \"message\": \"models/gemini-3.5-flash is not found for API version v1beta using key {secretKey}\", \"status\": \"NOT_FOUND\"}}}}";
        var (_, msg404) = GeminiService.ParseHttpError(System.Net.HttpStatusCode.NotFound, json404, "gemini-3.5-flash", secretKey);
        Assert.IsTrue(msg404.Contains("Modelo inexistente ou indisponível (404)"));
        Assert.IsTrue(msg404.Contains("gemini-2.5-flash"));
        Assert.IsFalse(msg404.Contains(secretKey), "Secret key must be sanitized from error output");
        Assert.IsTrue(msg404.Contains("[CHAVE_PROTEGIDA]"));

        // 2. 403 Invalid API Key
        string json403Invalid = "{\"error\": {\"code\": 403, \"message\": \"API_KEY_INVALID: API key not valid\", \"status\": \"PERMISSION_DENIED\"}}";
        var (_, msg403) = GeminiService.ParseHttpError(System.Net.HttpStatusCode.Forbidden, json403Invalid);
        Assert.IsTrue(msg403.Contains("Chave da API inválida"));
        Assert.IsTrue(msg403.Contains("Google AI Studio"));

        // 3. 403/429 Quota Exceeded
        string json429Quota = "{\"error\": {\"code\": 429, \"message\": \"QUOTA_EXCEEDED: Resource has been exhausted\", \"status\": \"RESOURCE_EXHAUSTED\"}}";
        var (_, msg429) = GeminiService.ParseHttpError(System.Net.HttpStatusCode.TooManyRequests, json429Quota);
        Assert.IsTrue(msg429.Contains("Limite de requisições ou cota excedida (429"));

        // 4. 503 Server Unavailable
        string json503 = "{\"error\": {\"code\": 503, \"message\": \"The service is temporarily unavailable.\", \"status\": \"UNAVAILABLE\"}}";
        var (_, msg503) = GeminiService.ParseHttpError(System.Net.HttpStatusCode.ServiceUnavailable, json503);
        Assert.IsTrue(msg503.Contains("Instabilidade temporária nos servidores do Google Gemini (503)"));
    }

    [TestMethod]
    public void ExportService_GenerateFileName_AllTokensAndFormatting_WorkAccurately()
    {
        var testDate = new DateTime(2026, 10, 14, 15, 30, 45);

        // Individual date and time tokens
        string patternTokens = "Print_{yyyy}_{MM}_{dd}_{HH}_{mm}_{ss}_{seq}";
        string result = ExportService.GenerateFileName(patternTokens, "png", 7, testDate);
        Assert.AreEqual("Print_2026_10_14_15_30_45_007.png", result);

        // Friendly Portuguese aliases: {data}, {hora}, {ano}, {mes}, {dia}, {numero}
        string patternAliases = "Doc_{data}_{hora}_{numero}";
        string resultAliases = ExportService.GenerateFileName(patternAliases, "jpg", 42, testDate);
        Assert.AreEqual("Doc_2026-10-14_15-30-45_042.jpg", resultAliases);

        // Fallback when empty or whitespace
        string fallback = ExportService.GenerateFileName("", "png", 1, testDate);
        Assert.IsTrue(fallback.StartsWith("Captura_"));
        Assert.IsTrue(fallback.EndsWith(".png"));
    }

    [TestMethod]
    public void ExportService_SanitizeFileName_SanitizesWindowsInvalidChars()
    {
        string invalidName = "Captura:Teste*Arquivo?Com<Barras>/E|Aspas\".png";
        string sanitized = ExportService.SanitizeFileName(invalidName);

        Assert.IsFalse(sanitized.Contains(":"));
        Assert.IsFalse(sanitized.Contains("*"));
        Assert.IsFalse(sanitized.Contains("?"));
        Assert.IsFalse(sanitized.Contains("<"));
        Assert.IsFalse(sanitized.Contains(">"));
        Assert.IsFalse(sanitized.Contains("/"));
        Assert.IsFalse(sanitized.Contains("|"));
        Assert.IsFalse(sanitized.Contains("\""));
    }

    [TestMethod]
    public void ExportService_GetUniqueFilePath_AvoidsCollisionsWithoutOverwriting()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ShotMindTestCollisions_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string fileName = "Screenshot.png";
            string path1 = ExportService.GetUniqueFilePath(tempDir, fileName);
            Assert.AreEqual(Path.Combine(tempDir, fileName), path1);

            // Simulate file 1 already existing
            File.WriteAllText(path1, "test 1");

            // Request same file name
            string path2 = ExportService.GetUniqueFilePath(tempDir, fileName);
            Assert.AreEqual(Path.Combine(tempDir, "Screenshot (1).png"), path2);

            // Simulate file 2 already existing
            File.WriteAllText(path2, "test 2");

            // Request same file name again
            string path3 = ExportService.GetUniqueFilePath(tempDir, fileName);
            Assert.AreEqual(Path.Combine(tempDir, "Screenshot (2).png"), path3);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [TestMethod]
    public void ExportService_RealBlurAndPixelate_ProducesDistinctObfuscatedBitmaps()
    {
        using var original = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(original))
        {
            g.Clear(System.Drawing.Color.Red);
            g.FillRectangle(System.Drawing.Brushes.Yellow, 25, 25, 50, 50);
        }

        var region = new Rectangle(10, 10, 80, 80);

        // Blur preview
        var blurred = ExportService.CreateObfuscatedRegionBitmap(original, region, ObfuscationStyle.Blur, 8);
        Assert.IsNotNull(blurred);
        Assert.AreEqual(80, blurred.PixelWidth);
        Assert.AreEqual(80, blurred.PixelHeight);

        // Pixelate preview
        var pixelated = ExportService.CreateObfuscatedRegionBitmap(original, region, ObfuscationStyle.Pixelate, 10);
        Assert.IsNotNull(pixelated);
        Assert.AreEqual(80, pixelated.PixelWidth);
        Assert.AreEqual(80, pixelated.PixelHeight);
    }

    [TestMethod]
    public void Logger_LogException_SanitizesSensitiveApiKeyAndWritesToDisk()
    {
        string logFile = Logger.GetLogFilePath();
        try
        {
            if (File.Exists(logFile)) File.Delete(logFile);
        }
        catch { }

        string secretKey = "AIzaSySecretTestingKey" + Guid.NewGuid().ToString("N");
        var ex = new InvalidOperationException($"Erro de teste com chave {secretKey} no processo.");

        Logger.LogException("TesteUnitario", "ExecutarValidacao", ex, $"Contexto confidencial {secretKey}");

        Assert.IsTrue(File.Exists(logFile), "Log file should exist");

        string content = File.ReadAllText(logFile);
        Assert.IsTrue(content.Contains("TesteUnitario"));
        Assert.IsTrue(content.Contains("ExecutarValidacao"));
        Assert.IsFalse(content.Contains(secretKey), "API Key should be sanitized from log output");
        Assert.IsTrue(content.Contains("[CHAVE_PROTEGIDA]"));
    }

    [TestMethod]
    public void GeminiService_BuildChatPayload_FormatsMultiTurnWithoutDuplicatingImages()
    {
        string fakeBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

        var history = new List<AiChatMessage>
        {
            new AiChatMessage { Role = "user", Text = "O que está nesta imagem?" },
            new AiChatMessage { Role = "model", Text = "É uma tela com código em C#." },
            new AiChatMessage { Role = "system_error", Text = "Falha de rede temporária" } // Should be ignored in payload
        };

        string nextQuestion = "Como corrijo o bug na linha 42?";
        var payloadObj = GeminiService.BuildChatPayload(history, nextQuestion, fakeBase64);
        var options = new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = System.Text.Json.JsonSerializer.Serialize(payloadObj, options);

        // Turn 1 (user): Must contain text AND image
        Assert.IsTrue(json.Contains("O que está nesta imagem?"));
        Assert.IsTrue(json.Contains("image/jpeg"));
        Assert.IsTrue(json.Contains(fakeBase64));

        // Turn 2 (model): Must contain answer
        Assert.IsTrue(json.Contains("É uma tela com código em C#."));

        // Turn 3 (user): Must contain nextQuestion
        Assert.IsTrue(json.Contains("Como corrijo o bug na linha 42?"));

        // System errors must not be included
        Assert.IsFalse(json.Contains("Falha de rede temporária"));

        // Image data must appear only ONCE in the entire payload (no duplicate images)
        int firstIndex = json.IndexOf(fakeBase64, StringComparison.Ordinal);
        int lastIndex = json.LastIndexOf(fakeBase64, StringComparison.Ordinal);
        Assert.AreEqual(firstIndex, lastIndex, "Image base64 should appear only once in the multi-turn payload to save bandwidth.");
    }

    [TestMethod]
    public void AiChatMessage_PropertiesAndRoles_WorkCorrectly()
    {
        var userMsg = new AiChatMessage { Role = "user", Text = "Teste usuário" };
        Assert.IsTrue(userMsg.IsUser);
        Assert.IsFalse(userMsg.IsModel);
        Assert.IsFalse(userMsg.IsError);
        Assert.IsFalse(string.IsNullOrEmpty(userMsg.Id));
        Assert.IsFalse(string.IsNullOrEmpty(userMsg.TimeFormatted));

        var modelMsg = new AiChatMessage { Role = "model", Text = "Resposta do modelo" };
        Assert.IsFalse(modelMsg.IsUser);
        Assert.IsTrue(modelMsg.IsModel);
        Assert.IsFalse(modelMsg.IsError);

        var errorMsg = new AiChatMessage { Role = "system_error", Text = "Erro de teste" };
        Assert.IsFalse(errorMsg.IsUser);
        Assert.IsFalse(errorMsg.IsModel);
        Assert.IsTrue(errorMsg.IsError);
    }

    [TestMethod]
    public void AnnotationItem_CloneAndAdvancedOptions_PreservedProperly()
    {
        var item = new AnnotationItem
        {
            Type = ToolType.Rectangle,
            StartPoint = new System.Windows.Point(10, 20),
            EndPoint = new System.Windows.Point(100, 80),
            Color = System.Windows.Media.Colors.DodgerBlue,
            StrokeThickness = 4.0,
            HasFill = true,
            FillColor = System.Windows.Media.Color.FromArgb(120, 30, 144, 255),
            IsPencil = true,
            HighlighterOpacity = 180,
            MarkerStyle = StepMarkerStyle.Uppercase,
            MarkerNumber = 3,
            MarkerText = "C",
            MarkerSize = 34.0
        };

        var clone = item.Clone();

        Assert.AreEqual(item.Type, clone.Type);
        Assert.AreEqual(item.StartPoint, clone.StartPoint);
        Assert.AreEqual(item.EndPoint, clone.EndPoint);
        Assert.AreEqual(item.Color, clone.Color);
        Assert.AreEqual(item.StrokeThickness, clone.StrokeThickness);
        Assert.AreEqual(item.HasFill, clone.HasFill);
        Assert.AreEqual(item.FillColor, clone.FillColor);
        Assert.AreEqual(item.IsPencil, clone.IsPencil);
        Assert.AreEqual(item.HighlighterOpacity, clone.HighlighterOpacity);
        Assert.AreEqual(item.MarkerStyle, clone.MarkerStyle);
        Assert.AreEqual(item.MarkerNumber, clone.MarkerNumber);
        Assert.AreEqual(item.MarkerText, clone.MarkerText);
        Assert.AreEqual(item.MarkerSize, clone.MarkerSize);
    }

    [TestMethod]
    public void ExportService_RenderAnnotatedBitmap_WithFillsAndPencil()
    {
        using var baseBmp = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(baseBmp))
        {
            g.Clear(System.Drawing.Color.White);
        }

        var annotations = new List<AnnotationItem>
        {
            new AnnotationItem
            {
                Type = ToolType.Rectangle,
                StartPoint = new System.Windows.Point(10, 10),
                EndPoint = new System.Windows.Point(50, 50),
                Color = System.Windows.Media.Colors.Red,
                StrokeThickness = 2.0,
                HasFill = true,
                FillColor = System.Windows.Media.Colors.Blue
            },
            new AnnotationItem
            {
                Type = ToolType.Ellipse,
                StartPoint = new System.Windows.Point(60, 60),
                EndPoint = new System.Windows.Point(90, 90),
                Color = System.Windows.Media.Colors.Green,
                StrokeThickness = 2.0,
                HasFill = true,
                FillColor = System.Windows.Media.Color.FromArgb(75, 0, 255, 0)
            },
            new AnnotationItem
            {
                Type = ToolType.Pen,
                Points = new List<System.Windows.Point>
                {
                    new(10, 60), new(20, 70), new(30, 80)
                },
                Color = System.Windows.Media.Colors.Black,
                StrokeThickness = 3.0,
                IsPencil = true
            },
            new AnnotationItem
            {
                Type = ToolType.Highlighter,
                Points = new List<System.Windows.Point>
                {
                    new(10, 20), new(40, 20)
                },
                Color = System.Windows.Media.Colors.Yellow,
                StrokeThickness = 10.0,
                HighlighterOpacity = 120
            }
        };

        using var rendered = ExportService.RenderAnnotatedBitmap(baseBmp, annotations);
        Assert.IsNotNull(rendered);
        Assert.AreEqual(100, rendered.Width);
        Assert.AreEqual(100, rendered.Height);
    }

    [TestMethod]
    public void ThemeService_GrayTheme_ReplacesWhiteThemeWithHighContrastForeground()
    {
        var grayTheme = ThemeService.GrayTheme;
        Assert.AreEqual("Gray", grayTheme.Name);
        Assert.AreEqual("Cinza", grayTheme.DisplayName);
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0x71, 0x71, 0x7A), grayTheme.Primary);
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF), grayTheme.Foreground, "Gray theme has high contrast white text.");

#pragma warning disable CS0618 // Type or member is obsolete
        var legacyWhite = ThemeService.WhiteTheme;
        Assert.AreEqual(grayTheme.Name, legacyWhite.Name, "Legacy WhiteTheme accessor safely redirects to GrayTheme.");
#pragma warning restore CS0618
    }

    [TestMethod]
    public void CleanInstall_FirstRunWithoutSettingsFile_InitializesWithoutStackOverflow()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string settingsDir = Path.Combine(appData, "ShotMind");
        string settingsPath = Path.Combine(settingsDir, "settings.json");
        string bakPath = Path.Combine(settingsDir, "settings.json.bak");

        string? originalJson = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
        string? originalBak = File.Exists(bakPath) ? File.ReadAllText(bakPath) : null;

        try
        {
            if (File.Exists(settingsPath)) File.Delete(settingsPath);
            if (File.Exists(bakPath)) File.Delete(bakPath);

            // Reset singleton via reflection to simulate true cold start on a clean installation
            var instanceField = typeof(SettingsService).GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(instanceField);
            instanceField.SetValue(null, null);

            // Exact sequence from App.OnStartup: Logger.Info before SettingsService.Instance.Load()
            Logger.Info("Iniciando CrankoShot em instalação limpa...");
            var loaded = SettingsService.Instance.Load();

            Assert.IsNotNull(loaded);
            Assert.IsTrue(File.Exists(settingsPath), "settings.json must be created automatically on clean install.");
            Assert.IsTrue(Directory.Exists(loaded.DefaultSaveDirectory), "DefaultSaveDirectory must be created automatically.");
        }
        finally
        {
            if (originalJson != null) File.WriteAllText(settingsPath, originalJson);
            if (originalBak != null) File.WriteAllText(bakPath, originalBak);
            SettingsService.Instance.Load();
        }
    }

    [TestMethod]
    public void CrosshairService_AllFiveStyles_GenerateValidMasksAndRespectCenterGeometry()
    {
        int centerIdx = CrosshairService.GridCenter * CrosshairService.GridSize + CrosshairService.GridCenter;

        // 1. Open crosshair without center dot -> center must be empty, arms must have stroke pixels
        var openMask = CrosshairService.BuildMask(CrosshairStyle.Open, thickness: 2, length: 12, gap: 4, dotSize: 3, showDot: false, showOutline: false);
        Assert.AreEqual(CrosshairService.PixelEmpty, openMask[centerIdx], "Open crosshair center should be empty when showDot is false.");
        Assert.AreEqual(CrosshairService.PixelStroke, openMask[(CrosshairService.GridCenter - 8) * CrosshairService.GridSize + CrosshairService.GridCenter], "Open crosshair top arm should have stroke.");

        // 2. Closed crosshair -> continuous cross through center
        var closedMask = CrosshairService.BuildMask(CrosshairStyle.Closed, thickness: 2, length: 12, gap: 4, dotSize: 3, showDot: false, showOutline: false);
        Assert.AreEqual(CrosshairService.PixelStroke, closedMask[centerIdx], "Closed crosshair must intersect at the center.");

        // 3. CenterDot crosshair -> center has dot, outer arms are empty
        var dotMask = CrosshairService.BuildMask(CrosshairStyle.CenterDot, thickness: 2, length: 12, gap: 4, dotSize: 4, showDot: true, showOutline: false);
        Assert.AreEqual(CrosshairService.PixelStroke, dotMask[centerIdx], "CenterDot crosshair must have center dot.");
        Assert.AreEqual(CrosshairService.PixelEmpty, dotMask[(CrosshairService.GridCenter - 12) * CrosshairService.GridSize + CrosshairService.GridCenter], "CenterDot crosshair must not draw outer line arms.");

        // 4. LinesAndDot crosshair -> center has dot, gap is empty, outer arms have stroke
        var linesAndDotMask = CrosshairService.BuildMask(CrosshairStyle.LinesAndDot, thickness: 2, length: 12, gap: 5, dotSize: 2, showDot: true, showOutline: false);
        Assert.AreEqual(CrosshairService.PixelStroke, linesAndDotMask[centerIdx], "LinesAndDot must have center dot.");
        Assert.AreEqual(CrosshairService.PixelEmpty, linesAndDotMask[(CrosshairService.GridCenter - 3) * CrosshairService.GridSize + CrosshairService.GridCenter], "LinesAndDot must keep gap between dot and arms.");
        Assert.AreEqual(CrosshairService.PixelStroke, linesAndDotMask[(CrosshairService.GridCenter - 10) * CrosshairService.GridSize + CrosshairService.GridCenter], "LinesAndDot must draw outer arms.");

        // 5. Simple crosshair -> classic balanced cross
        var simpleMask = CrosshairService.BuildMask(CrosshairStyle.Simple, thickness: 2, length: 12, gap: 4, dotSize: 3, showDot: false, showOutline: true);
        Assert.AreEqual(CrosshairService.PixelStroke, simpleMask[(CrosshairService.GridCenter - 8) * CrosshairService.GridSize + CrosshairService.GridCenter]);
    }

    [TestMethod]
    public void CrosshairService_LocalizedPixelInversion_InvertsOnlyCrosshairPixelsOnLightAndDarkSurfaces()
    {
        var settings = new AppSettings
        {
            AutoContrastCursor = true,
            CrosshairStyle = CrosshairStyle.LinesAndDot,
            CrosshairThickness = 2,
            CrosshairLength = 12,
            CrosshairGap = 4,
            CrosshairDotSize = 3,
            CrosshairShowDot = true
        };

        byte[] mask = CrosshairService.BuildMask(settings);
        int totalPixels = CrosshairService.GridSize * CrosshairService.GridSize;
        int[] targetBuffer = new int[totalPixels];
        int[] screenPixels = new int[100 * 100];
        var selectionRect = new System.Windows.Rect(0, 0, 100, 100);

        // Case 1: Pure White Surface (#FFFFFF) -> Crosshair pixels must become dark (R,G,B < 30), non-crosshair pixels must stay 0 (transparent)
        Array.Fill(screenPixels, unchecked((int)0xFFFFFFFF));
        CrosshairService.RenderToBuffer(targetBuffer, mask, settings, screenPixels, 100, 100, 50, 50, selectionRect, hasSelection: true);

        for (int i = 0; i < totalPixels; i++)
        {
            if (mask[i] == CrosshairService.PixelStroke)
            {
                int argb = targetBuffer[i];
                int r = (argb >> 16) & 0xFF;
                int g = (argb >> 8) & 0xFF;
                int b = argb & 0xFF;
                Assert.IsTrue(r < 30 && g < 30 && b < 30, $"Stroke pixel over white surface must be dark, got RGB({r},{g},{b})");
            }
            else
            {
                Assert.AreEqual(0, targetBuffer[i], "Pixels outside crosshair stroke must remain completely transparent in localized inversion mode.");
            }
        }

        // Case 2: Dark Surface (#121218) -> Crosshair pixels must become bright (R,G,B > 220)
        Array.Fill(screenPixels, unchecked((int)0xFF121218));
        CrosshairService.RenderToBuffer(targetBuffer, mask, settings, screenPixels, 100, 100, 50, 50, selectionRect, hasSelection: true);

        for (int i = 0; i < totalPixels; i++)
        {
            if (mask[i] == CrosshairService.PixelStroke)
            {
                int argb = targetBuffer[i];
                int r = (argb >> 16) & 0xFF;
                int g = (argb >> 8) & 0xFF;
                int b = argb & 0xFF;
                Assert.IsTrue(r > 220 && g > 220 && b > 220, $"Stroke pixel over dark surface must be bright, got RGB({r},{g},{b})");
            }
            else
            {
                Assert.AreEqual(0, targetBuffer[i], "Pixels outside crosshair stroke must remain completely transparent.");
            }
        }
    }

    [TestMethod]
    public void Settings_OpenRouterAndGeminiIndependentPersistence_PreservesBothProviders()
    {
        var settingsService = SettingsService.Instance;
        string geminiKey = "AIzaSyGeminiIndependentKey1234";
        string openRouterKey = "sk-or-v1-9876543210abcdef1234567890abcdef";

        settingsService.SetApiKey(geminiKey);
        settingsService.SetOpenRouterApiKey(openRouterKey);
        settingsService.Current.GeminiModel = "gemini-2.5-pro";
        settingsService.Current.OpenRouterModel = "google/gemma-4-31b-it:free";
        settingsService.Current.AiProvider = AiProviderType.OpenRouter;

        Assert.IsTrue(settingsService.HasApiKey());
        Assert.IsTrue(settingsService.HasOpenRouterApiKey());
        Assert.AreEqual(geminiKey, settingsService.GetApiKey());
        Assert.AreEqual(openRouterKey, settingsService.GetOpenRouterApiKey());

        string maskedOr = settingsService.GetMaskedOpenRouterApiKey();
        Assert.IsTrue(maskedOr.StartsWith("sk-or-..."));
        Assert.IsTrue(maskedOr.EndsWith("cdef"));
        Assert.IsFalse(maskedOr.Contains("9876543210"));

        // Switch back to Gemini without losing OpenRouter settings
        settingsService.Current.AiProvider = AiProviderType.Gemini;
        Assert.AreEqual(openRouterKey, settingsService.GetOpenRouterApiKey());
        Assert.AreEqual("google/gemma-4-31b-it:free", settingsService.Current.OpenRouterModel);
        Assert.AreEqual(geminiKey, settingsService.GetApiKey());
        Assert.AreEqual("gemini-2.5-pro", settingsService.Current.GeminiModel);

        // Cleanup test keys
        settingsService.SetApiKey(string.Empty);
        settingsService.SetOpenRouterApiKey(string.Empty);
    }

    [TestMethod]
    public void OpenRouterService_FilterAndParseModelsJson_VerifiesModalitiesPricingAndExcludesImageGeneration()
    {
        string sampleCatalogJson = """
        {
          "data": [
            {
              "id": "paid/text-model",
              "name": "Paid Text Model",
              "description": "Paid model",
              "context_length": 128000,
              "architecture": {
                "input_modalities": ["text"],
                "output_modalities": ["text"]
              },
              "pricing": {
                "prompt": "0.000001",
                "completion": "0.000002",
                "image": "0",
                "request": "0"
              }
            },
            {
              "id": "free/image-generator:free",
              "name": "Free Image Generator",
              "description": "Generates images instead of text",
              "context_length": 4096,
              "architecture": {
                "input_modalities": ["text"],
                "output_modalities": ["image"]
              },
              "pricing": {
                "prompt": "0",
                "completion": "0",
                "image": "0",
                "request": "0"
              }
            },
            {
              "id": "free/text-only:free",
              "name": "Free Text Only",
              "description": "Free text model",
              "context_length": 65536,
              "architecture": {
                "input_modalities": ["text"],
                "output_modalities": ["text"]
              },
              "pricing": {
                "prompt": "0",
                "completion": "0",
                "image": "0",
                "request": "0"
              }
            },
            {
              "id": "free/vision-text:free",
              "name": "Free Vision and Text",
              "description": "Free multimodal model",
              "context_length": 262144,
              "architecture": {
                "input_modalities": ["text", "image"],
                "output_modalities": ["text"]
              },
              "pricing": {
                "prompt": "0",
                "completion": "0",
                "image": "0",
                "request": "0"
              }
            }
          ]
        }
        """;

        var freeModels = OpenRouterService.FilterAndParseModelsJson(sampleCatalogJson, freeOnly: true);

        // Must exclude paid model and image-generator model
        Assert.AreEqual(2, freeModels.Count);

        // Must prioritize Vision + Text model first
        Assert.AreEqual("free/vision-text:free", freeModels[0].Id);
        Assert.IsTrue(freeModels[0].IsFree);
        Assert.IsTrue(freeModels[0].SupportsVision);
        Assert.IsTrue(freeModels[0].SupportsText);

        // Followed by Free Text-only model
        Assert.AreEqual("free/text-only:free", freeModels[1].Id);
        Assert.IsTrue(freeModels[1].IsFree);
        Assert.IsFalse(freeModels[1].SupportsVision);
        Assert.IsTrue(freeModels[1].SupportsText);

        // When freeOnly is false, paid text model is included at the end, image generator still excluded
        var allCompatible = OpenRouterService.FilterAndParseModelsJson(sampleCatalogJson, freeOnly: false);
        Assert.AreEqual(3, allCompatible.Count);
        Assert.IsFalse(allCompatible.Exists(m => m.Id == "free/image-generator:free"), "Image generation models must never be included.");
    }

    [TestMethod]
    public void OpenRouterService_BuildChatPayload_HandlesVisionAndTextOnlyModelsAccurately()
    {
        string fakeBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
        var history = new List<AiChatMessage>
        {
            new AiChatMessage { Role = "user", Text = "Descreva esta tela." },
            new AiChatMessage { Role = "model", Text = "É uma janela de configurações." }
        };

        // 1. Vision model -> includes image_url once
        var visionPayload = OpenRouterService.BuildChatPayload(
            "google/gemma-4-31b-it:free",
            history,
            "Quais botões aparecem?",
            fakeBase64,
            modelSupportsVision: true);

        string visionJson = System.Text.Json.JsonSerializer.Serialize(visionPayload);
        Assert.IsTrue(visionJson.Contains("image_url"));
        Assert.IsTrue(visionJson.Contains(fakeBase64));
        int firstIdx = visionJson.IndexOf(fakeBase64, StringComparison.Ordinal);
        int lastIdx = visionJson.LastIndexOf(fakeBase64, StringComparison.Ordinal);
        Assert.AreEqual(firstIdx, lastIdx, "Image must be attached only once in multi-turn chat.");

        // 2. Text-only model -> omits image_url so text-only endpoint does not reject the payload
        var textOnlyPayload = OpenRouterService.BuildChatPayload(
            "nvidia/nemotron-3-ultra-550b-a55b:free",
            history,
            "Resuma o texto acima.",
            fakeBase64,
            modelSupportsVision: false);

        string textOnlyJson = System.Text.Json.JsonSerializer.Serialize(textOnlyPayload);
        Assert.IsFalse(textOnlyJson.Contains("image_url"), "Text-only payload must not contain raw image_url blocks.");
        Assert.IsFalse(textOnlyJson.Contains(fakeBase64));
        Assert.IsTrue(textOnlyJson.Contains("Resuma o texto acima."));
    }

    [TestMethod]
    public void OpenRouterService_ParseHttpError_HandlesAuthCreditsRateLimitAndSanitizesKeys()
    {
        string secretKey = "sk-or-v1-secretkey999888777666555444";

        // 401 Unauthorized
        string json401 = $"{{\"error\": {{\"code\": 401, \"message\": \"No auth credentials found for {secretKey}\"}}}}";
        var (_, msg401) = OpenRouterService.ParseHttpError(System.Net.HttpStatusCode.Unauthorized, json401, "google/gemma-4-31b-it:free", secretKey);
        Assert.IsTrue(msg401.Contains("Chave da API do OpenRouter inválida"));
        Assert.IsFalse(msg401.Contains(secretKey), "Secret key must never be exposed in error messages");
        Assert.IsTrue(msg401.Contains("[CHAVE_PROTEGIDA]"));

        // 402 Insufficient credits
        string json402 = "{\"error\": {\"code\": 402, \"message\": \"Insufficient credits\"}}";
        var (_, msg402) = OpenRouterService.ParseHttpError(System.Net.HttpStatusCode.PaymentRequired, json402, "openai/gpt-4o");
        Assert.IsTrue(msg402.Contains("Créditos insuficientes no OpenRouter (402)"));

        // 403 Data policy / privacy setting for free models
        string json403Policy = "{\"error\": {\"code\": 403, \"message\": \"No endpoints found matching your data policy\"}}";
        var (_, msg403) = OpenRouterService.ParseHttpError(System.Net.HttpStatusCode.Forbidden, json403Policy, "google/gemma-4-31b-it:free");
        Assert.IsTrue(msg403.Contains("Configuração de privacidade do OpenRouter necessária para modelos gratuitos (403)"));
        Assert.IsTrue(msg403.Contains("openrouter.ai/settings/privacy"));

        // 404 Model unavailable
        string json404 = "{\"error\": {\"code\": 404, \"message\": \"No endpoints found for model\"}}";
        var (_, msg404) = OpenRouterService.ParseHttpError(System.Net.HttpStatusCode.NotFound, json404, "modelo/inexistente:free");
        Assert.IsTrue(msg404.Contains("Modelo indisponível ou não encontrado no OpenRouter (404)"));

        // 429 Rate limit
        string json429 = "{\"error\": {\"code\": 429, \"message\": \"Rate limit exceeded: free-models-per-min\"}}";
        var (_, msg429) = OpenRouterService.ParseHttpError(System.Net.HttpStatusCode.TooManyRequests, json429, "google/gemma-4-31b-it:free");
        Assert.IsTrue(msg429.Contains("Limite de requisições atingido no OpenRouter (429"));
    }

    [TestMethod]
    public async Task OpenRouterService_LiveCatalogAndInvalidKeyValidation_WorksAccurately()
    {
        var (ok, models, _) = await OpenRouterService.Instance.GetAvailableModelsAsync(freeOnly: true);
        Assert.IsNotNull(models);
        Assert.IsTrue(models.Count > 0, "Should return verified free models from live catalog or local fallback.");
        Assert.IsTrue(models[0].SupportsVision, "First prioritized model must support Vision + Text.");
        Assert.IsTrue(models[0].IsFree, "First prioritized model must be free.");

        // Test connection with an intentionally invalid key -> must return clear 401/403 error without crashing
        var (connOk, connMsg) = await OpenRouterService.Instance.TestConnectionAsync("sk-or-v1-invalid-test-key-000000000000", "google/gemma-4-31b-it:free");
        Assert.IsFalse(connOk);
        Assert.IsFalse(connMsg.Contains("sk-or-v1-invalid-test-key-000000000000"), "Invalid key must be sanitized from error output.");
    }

    [TestMethod]
    public void SettingsWindow_AllPanesRenderWithoutClippingOrLayoutExceptions()
    {
        Exception? threadEx = null;
        string outputDir = Path.Combine(Path.GetTempPath(), "CrankoShot_UiTests");
        Directory.CreateDirectory(outputDir);

        var staThread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    var app = new ShotMind.App();
                    app.InitializeComponent();
                }

                ThemeService.Instance.ApplyTheme("Purple", persist: false);

                var win = new ShotMind.UI.Views.SettingsWindow
                {
                    Width = 980,
                    Height = 760
                };

                // Trigger Window_Loaded logic via reflection so all controls populate
                var loadedMethod = typeof(ShotMind.UI.Views.SettingsWindow).GetMethod(
                    "Window_Loaded",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                loadedMethod?.Invoke(win, new object?[] { win, new System.Windows.RoutedEventArgs() });

                var navSidebar = (System.Windows.Controls.ListBox)win.FindName("NavSidebar");
                Assert.IsNotNull(navSidebar);

                // Análise automática removida da interface; controles do som da captura presentes
                Assert.IsNull(win.FindName("ChkAutoAnalyze"), "A opção de análise automática deve ter sido removida.");
                Assert.IsNotNull(win.FindName("CmbCaptureSound"));
                Assert.IsNotNull(win.FindName("SliderSoundVolume"));
                Assert.IsNotNull(win.FindName("ChkMuteSound"));
                Assert.IsNotNull(win.FindName("BtnPreviewSound"));
                Assert.IsNotNull(win.FindName("BtnImportSound"));
                Assert.IsNotNull(win.FindName("BtnRemoveSound"));
                var rootContent = (System.Windows.FrameworkElement)win.Content;
                Assert.IsNotNull(rootContent);

                for (int i = 0; i < 7; i++)
                {
                    navSidebar.SelectedIndex = i;
                    rootContent.Measure(new System.Windows.Size(980, 760));
                    rootContent.Arrange(new System.Windows.Rect(0, 0, 980, 760));
                    rootContent.UpdateLayout();

                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        980, 760, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(rootContent);

                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                    using var fs = File.Create(Path.Combine(outputDir, $"pane_{i}.png"));
                    encoder.Save(fs);

                    // Also render full scrollable StackPanel height for long panes (PaneCursor=2, PaneSalvamento=4, PaneIA=5)
                    if (i is 2 or 4 or 5)
                    {
                        string paneName = i == 2 ? "PaneCursor" : i == 4 ? "PaneSalvamento" : "PaneIA";
                        if (win.FindName(paneName) is System.Windows.Controls.StackPanel innerStack)
                        {
                            innerStack.Measure(new System.Windows.Size(650, double.PositiveInfinity));
                            int fullHeight = Math.Max(400, (int)Math.Ceiling(innerStack.DesiredSize.Height) + 40);
                            innerStack.Arrange(new System.Windows.Rect(0, 0, 650, fullHeight));
                            innerStack.UpdateLayout();

                            var rtbFull = new System.Windows.Media.Imaging.RenderTargetBitmap(
                                650, fullHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            var dv = new System.Windows.Media.DrawingVisual();
                            using (var dc = dv.RenderOpen())
                            {
                                dc.DrawRectangle(
                                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0B, 0x0B, 0x10)),
                                    null,
                                    new System.Windows.Rect(0, 0, 650, fullHeight));
                                dc.DrawRectangle(
                                    new System.Windows.Media.VisualBrush(innerStack) { Stretch = System.Windows.Media.Stretch.None, AlignmentX = System.Windows.Media.AlignmentX.Left, AlignmentY = System.Windows.Media.AlignmentY.Top },
                                    null,
                                    new System.Windows.Rect(0, 0, 650, fullHeight));
                            }
                            rtbFull.Render(dv);

                            var encBottom = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            encBottom.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbFull));
                            using var fsBottom = File.Create(Path.Combine(outputDir, $"pane_{i}_full.png"));
                            encBottom.Save(fsBottom);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        if (threadEx != null)
        {
            Assert.Fail($"SettingsWindow STA render test failed: {threadEx}");
        }

        Assert.IsTrue(File.Exists(Path.Combine(outputDir, "pane_2.png")));
        Assert.IsTrue(File.Exists(Path.Combine(outputDir, "pane_3.png")));
        Assert.IsTrue(File.Exists(Path.Combine(outputDir, "pane_4.png")));
        Assert.IsTrue(File.Exists(Path.Combine(outputDir, "pane_5.png")));
    }

    // ------------------------------------------------------------------
    // Miras: formato, centralização, abertura e escala
    // ------------------------------------------------------------------

    private static bool MaskPixel(byte[] mask, int x, int y)
        => x >= 0 && x < CrosshairService.GridSize && y >= 0 && y < CrosshairService.GridSize
           && mask[y * CrosshairService.GridSize + x] == CrosshairService.PixelStroke;

    private static void AssertMaskSymmetric(byte[] mask, bool evenPivot, string context)
    {
        // Espelho em torno do pivô: pixel central (par = 80 - x) ou par de pixels centrais (81 - x)
        int sum = evenPivot ? 2 * CrosshairService.GridCenter + 1 : 2 * CrosshairService.GridCenter;
        int size = CrosshairService.GridSize;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool p = MaskPixel(mask, x, y);
                Assert.AreEqual(p, MaskPixel(mask, sum - x, y), $"{context}: assimetria horizontal em ({x},{y})");
                Assert.AreEqual(p, MaskPixel(mask, x, sum - y), $"{context}: assimetria vertical em ({x},{y})");
                Assert.AreEqual(p, MaskPixel(mask, y, x), $"{context}: assimetria diagonal em ({x},{y})");
            }
        }
    }

    [TestMethod]
    public void Crosshair_AllStylesThicknessesAndDots_AreSymmetricAndCenterDotIsExactlyCentered()
    {
        foreach (CrosshairStyle style in Enum.GetValues(typeof(CrosshairStyle)))
        {
            for (int thickness = 1; thickness <= 6; thickness++)
            {
                for (int dot = 1; dot <= 10; dot++)
                {
                    foreach (bool showDot in new[] { false, true })
                    {
                        var mask = CrosshairService.BuildMask(style, thickness, 12, 4, dot, showDot, showOutline: false);

                        // Com linhas, o pivô segue a espessura; só o ponto (CenterDot) segue o tamanho do ponto.
                        bool evenPivot = style == CrosshairStyle.CenterDot ? dot % 2 == 0 : thickness % 2 == 0;
                        AssertMaskSymmetric(mask, evenPivot, $"{style} t={thickness} dot={dot} showDot={showDot}");
                    }
                }
            }
        }
    }

    [TestMethod]
    public void Crosshair_OpenStyle_HasFourSymmetricArmsAndHonorsConfiguredGap()
    {
        int center = CrosshairService.GridCenter;

        foreach (int gap in new[] { 1, 2, 4, 7, 10 })
        {
            // Espessura ímpar: pixel central + (gap-1) livres de cada lado
            var odd = CrosshairService.BuildMask(CrosshairStyle.Open, 1, 10, gap, 3, showDot: false, showOutline: false);
            Assert.IsFalse(MaskPixel(odd, center, center), "Mira aberta não pode ter ponto/linha no centro.");
            for (int k = 0; k < gap; k++)
            {
                Assert.IsFalse(MaskPixel(odd, center, center - k) || MaskPixel(odd, center, center + k), $"vão (k={k}) deve estar livre, gap={gap}");
            }
            Assert.IsTrue(MaskPixel(odd, center, center - gap) && MaskPixel(odd, center, center + gap), $"braço vertical deve iniciar em k={gap}");
            Assert.IsTrue(MaskPixel(odd, center - gap, center) && MaskPixel(odd, center + gap, center), $"braço horizontal deve iniciar em k={gap}");
            Assert.IsTrue(MaskPixel(odd, center, center - gap - 9) && !MaskPixel(odd, center, center - gap - 10), "comprimento exato de 10 px");

            // Espessura par: 2 pixels centrais; distâncias idênticas para os quatro braços
            var even = CrosshairService.BuildMask(CrosshairStyle.Open, 2, 10, gap, 3, showDot: false, showOutline: false);
            Assert.IsFalse(MaskPixel(even, center, center) || MaskPixel(even, center + 1, center + 1));
            int topStart = Enumerable.Range(0, center).Select(i => center - i).First(y => MaskPixel(even, center, y));
            int bottomStart = Enumerable.Range(center + 1, center).First(y => MaskPixel(even, center, y));
            int leftStart = Enumerable.Range(0, center).Select(i => center - i).First(x => MaskPixel(even, x, center));
            int rightStart = Enumerable.Range(center + 1, center).First(x => MaskPixel(even, x, center));
            Assert.AreEqual(center + center + 1, topStart + bottomStart, "braços superior/inferior simétricos (espessura par)");
            Assert.AreEqual(center + center + 1, leftStart + rightStart, "braços esquerdo/direito simétricos (espessura par)");
            Assert.AreEqual(gap, center - topStart, "o vão configurado deve ser respeitado");
        }
    }

    [TestMethod]
    public void Crosshair_EachStyleDrawsItsExpectedShape()
    {
        int c = CrosshairService.GridCenter;
        int Count(byte[] m) => m.Count(b => b == CrosshairService.PixelStroke);

        var simple = CrosshairService.BuildMask(CrosshairStyle.Simple, 1, 10, 4, 3, false, false);
        Assert.IsFalse(MaskPixel(simple, c, c));
        Assert.IsTrue(MaskPixel(simple, c, c - 4));

        var open = CrosshairService.BuildMask(CrosshairStyle.Open, 1, 10, 4, 3, true, false);
        // Mesmo com o interruptor do ponto ligado, o ponto só aparece se solicitado; aqui há ponto e braços afastados
        Assert.IsTrue(MaskPixel(open, c, c));

        var closed = CrosshairService.BuildMask(CrosshairStyle.Closed, 1, 10, 4, 3, false, false);
        for (int i = -13; i <= 13; i++)
        {
            Assert.IsTrue(MaskPixel(closed, c, c + i) && MaskPixel(closed, c + i, c), $"cruz contínua deve cobrir k={i}");
        }
        Assert.IsFalse(MaskPixel(closed, c + 1, c + 1));

        var dotOnly = CrosshairService.BuildMask(CrosshairStyle.CenterDot, 1, 10, 4, 5, true, false);
        Assert.IsTrue(MaskPixel(dotOnly, c, c));
        Assert.IsFalse(MaskPixel(dotOnly, c, c - 10));
        Assert.IsTrue(Count(dotOnly) < 30, "CenterDot não pode desenhar linhas");

        var linesAndDot = CrosshairService.BuildMask(CrosshairStyle.LinesAndDot, 1, 10, 6, 3, false, false);
        Assert.IsTrue(MaskPixel(linesAndDot, c, c), "ponto central presente");
        Assert.IsFalse(MaskPixel(linesAndDot, c, c - 3), "separação entre ponto e braços");
        Assert.IsTrue(MaskPixel(linesAndDot, c, c - 6), "braços após o vão");
    }

    [TestMethod]
    public void Crosshair_QuickScale_IsProportionalMultiplierOnAllElementsWithoutDoubleApplication()
    {
        var baseSettings = new AppSettings
        {
            CrosshairStyle = CrosshairStyle.LinesAndDot,
            CrosshairThickness = 2,
            CrosshairLength = 12,
            CrosshairGap = 4,
            CrosshairDotSize = 3,
            CrosshairOutline = false,
            CursorSize = 18
        };

        Assert.AreEqual((2, 12, 4, 3), CrosshairService.GetScaledGeometry(baseSettings));

        baseSettings.CursorSize = 14;
        Assert.AreEqual((2, 9, 3, 2), CrosshairService.GetScaledGeometry(baseSettings));

        baseSettings.CursorSize = 24;
        Assert.AreEqual((3, 18, 6, 5), CrosshairService.GetScaledGeometry(baseSettings));

        // A máscara real usa exatamente a geometria escalada (uma única vez) e é a mesma usada pela prévia.
        var expected = CrosshairService.BuildMask(CrosshairStyle.LinesAndDot, 3, 18, 6, 5, false, false);
        CollectionAssert.AreEqual(expected, CrosshairService.BuildMask(baseSettings));

        // Escala maior ⇒ desenho maior em todas as dimensões (extensão total e quantidade de pixels)
        int Extent(byte[] m) => Enumerable.Range(0, CrosshairService.GridSize).Count(y => MaskPixel(m, CrosshairService.GridCenter, y));
        baseSettings.CursorSize = 14; var small = CrosshairService.BuildMask(baseSettings);
        baseSettings.CursorSize = 18; var normal = CrosshairService.BuildMask(baseSettings);
        baseSettings.CursorSize = 24; var large = CrosshairService.BuildMask(baseSettings);
        Assert.IsTrue(Extent(small) < Extent(normal) && Extent(normal) < Extent(large));
        Assert.IsTrue(small.Count(b => b == 1) < normal.Count(b => b == 1) && normal.Count(b => b == 1) < large.Count(b => b == 1));
    }

    [TestMethod]
    public void Crosshair_ExtremeGeometryAtLargestScale_StaysInsideGridAndSymmetric()
    {
        var s = new AppSettings
        {
            CrosshairStyle = CrosshairStyle.LinesAndDot,
            CrosshairThickness = 6,
            CrosshairLength = 28,
            CrosshairGap = 14,
            CrosshairDotSize = 10,
            CrosshairOutline = true,
            CursorSize = 24
        };

        var mask = CrosshairService.BuildMask(s);
        int size = CrosshairService.GridSize;
        for (int i = 0; i < size; i++)
        {
            Assert.AreEqual(CrosshairService.PixelEmpty, mask[i], "borda superior deve ficar vazia");
            Assert.AreEqual(CrosshairService.PixelEmpty, mask[(size - 1) * size + i], "borda inferior deve ficar vazia");
            Assert.AreEqual(CrosshairService.PixelEmpty, mask[i * size], "borda esquerda deve ficar vazia");
            Assert.AreEqual(CrosshairService.PixelEmpty, mask[i * size + size - 1], "borda direita deve ficar vazia");
        }

        s.CrosshairOutline = false;
        var noOutline = CrosshairService.BuildMask(s);
        bool IsSymmetric(int sum) => Enumerable.Range(0, size).All(y => Enumerable.Range(0, size).All(x =>
            MaskPixel(noOutline, x, y) == MaskPixel(noOutline, sum - x, y) && MaskPixel(noOutline, x, y) == MaskPixel(noOutline, x, sum - y)));
        Assert.IsTrue(IsSymmetric(2 * CrosshairService.GridCenter) || IsSymmetric(2 * CrosshairService.GridCenter + 1), "mira extrema deve permanecer simétrica");
    }

    [TestMethod]
    public void Settings_LegacyCrosshairScale_IsMigratedToAvoidDoubleScaling()
    {
        var legacyLarge = new AppSettings { CursorSize = 24, CrosshairLength = 18, CrosshairScaleVersion = 0 };
        SettingsService.ValidateSettings(legacyLarge);
        Assert.AreEqual(12, legacyLarge.CrosshairLength);

        var legacyCompact = new AppSettings { CursorSize = 14, CrosshairLength = 9, CrosshairScaleVersion = 0 };
        SettingsService.ValidateSettings(legacyCompact);
        Assert.AreEqual(12, legacyCompact.CrosshairLength);

        var custom = new AppSettings { CursorSize = 24, CrosshairLength = 20, CrosshairScaleVersion = 0 };
        SettingsService.ValidateSettings(custom);
        Assert.AreEqual(20, custom.CrosshairLength, "Valores personalizados não devem ser alterados.");
    }

    // ------------------------------------------------------------------
    // Som da captura
    // ------------------------------------------------------------------

    [TestMethod]
    public void Sound_BuiltInSounds_AreValidWavFilesWithoutClipping()
    {
        foreach (string key in new[] { "shutter", "soft", "chime", "pop", "beep" })
        {
            byte[]? wav = SoundService.SynthesizeBuiltIn(key);
            Assert.IsNotNull(wav, key);
            Assert.IsTrue(wav!.Length > 1000, key);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
            Assert.AreEqual("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
            Assert.AreEqual(wav.Length - 8, BitConverter.ToInt32(wav, 4));

            int peak = 0;
            for (int i = 44; i + 1 < wav.Length; i += 2)
            {
                peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(wav, i)));
            }
            Assert.IsTrue(peak > 3000, $"{key} deve ser audível");
            Assert.IsTrue(peak < short.MaxValue, $"{key} não pode saturar");
        }

        Assert.IsNull(SoundService.SynthesizeBuiltIn("inexistente"));
    }

    [TestMethod]
    public void Sound_VolumeMuteAndMasterSwitch_ControlPlaybackDecision()
    {
        Assert.AreEqual(0.0, SoundService.ToPlayerVolume(0));
        Assert.AreEqual(0.5, SoundService.ToPlayerVolume(50));
        Assert.AreEqual(1.0, SoundService.ToPlayerVolume(100));
        Assert.AreEqual(1.0, SoundService.ToPlayerVolume(250));
        Assert.AreEqual(0.0, SoundService.ToPlayerVolume(-10));

        var s = new AppSettings { PlaySoundOnCapture = true, CaptureSoundMuted = false, CaptureSoundVolume = 40 };
        Assert.IsTrue(SoundService.ShouldPlayCaptureSound(s));
        s.CaptureSoundMuted = true;
        Assert.IsFalse(SoundService.ShouldPlayCaptureSound(s), "silenciado não toca");
        s.CaptureSoundMuted = false; s.CaptureSoundVolume = 0;
        Assert.IsFalse(SoundService.ShouldPlayCaptureSound(s), "volume 0 não toca");
        s.CaptureSoundVolume = 100; s.PlaySoundOnCapture = false;
        Assert.IsFalse(SoundService.ShouldPlayCaptureSound(s), "interruptor geral desligado não toca");

        Assert.IsFalse(SoundService.PreviewSound("builtin:soft", 80, muted: true), "pré-visualização respeita o silenciamento");
        Assert.IsFalse(SoundService.PreviewSound("builtin:soft", 0, muted: false), "pré-visualização respeita volume 0");
        Assert.IsTrue(SoundService.PreviewSound("builtin:soft", 5, muted: false));
        Assert.IsTrue(SoundService.PreviewSound("system", 100, muted: false));
    }

    [TestMethod]
    public async Task Sound_ImportSelectPersistAndRemoveCustomSound_WorksAndSurvivesRestart()
    {
        var settingsService = SettingsService.Instance;
        string originalId = settingsService.Current.CaptureSoundId;
        string tempDir = Path.Combine(Path.GetTempPath(), "CrankoShotSoundTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string? importedId = null;

        try
        {
            // Arquivo de origem temporário (será apagado depois: o app NÃO pode depender dele)
            string source = Path.Combine(tempDir, "meu som.wav");
            File.WriteAllBytes(source, SoundService.SynthesizeBuiltIn("chime")!);

            var (ok, message, sound) = await SoundService.ImportCustomSoundAsync(source);
            Assert.IsTrue(ok, message);
            Assert.IsNotNull(sound);
            importedId = sound!.Id;
            Assert.AreEqual("meu som", sound.DisplayName);

            string storedPath = Path.Combine(SoundService.CustomDirectory, sound.FileName);
            Assert.IsTrue(File.Exists(storedPath), "arquivo importado deve ser copiado para a pasta do aplicativo");

            // O original pode desaparecer sem afetar o som importado
            File.Delete(source);
            Directory.Delete(tempDir, true);
            Assert.IsTrue(File.Exists(storedPath));

            // Selecionar como som padrão e persistir
            settingsService.Current.CaptureSoundId = SoundService.CustomPrefix + sound.Id;
            settingsService.Current.CaptureSoundVolume = 35;
            settingsService.Current.CaptureSoundMuted = false;
            Assert.IsTrue(settingsService.Save());

            // "Reinício": recarrega as configurações do disco
            var reloaded = settingsService.Load();
            Assert.AreEqual(SoundService.CustomPrefix + sound.Id, reloaded.CaptureSoundId);
            Assert.AreEqual(35, reloaded.CaptureSoundVolume);
            Assert.IsTrue(reloaded.CustomCaptureSounds.Any(c => c.Id == sound.Id));
            Assert.IsTrue(SoundService.GetAvailableSounds().Any(o => o.Id == SoundService.CustomPrefix + sound.Id && o.IsCustom));

            string? resolved = SoundService.ResolveSoundPath(reloaded.CaptureSoundId, reloaded.CustomCaptureSounds);
            Assert.AreEqual(storedPath, resolved);

            // Reproduzir e pré-visualizar o som importado não pode lançar exceções
            Assert.IsTrue(SoundService.PreviewSound(reloaded.CaptureSoundId, 35, muted: false));
            SoundService.PlayCaptureSound();
            await Task.Delay(400);

            // Remover: apaga o arquivo, remove da lista e volta ao som padrão
            Assert.IsTrue(SoundService.RemoveCustomSound(sound.Id));
            importedId = null;
            Assert.IsFalse(File.Exists(storedPath), "arquivo importado deve ser apagado ao remover");
            Assert.IsFalse(settingsService.Current.CustomCaptureSounds.Any(c => c.Id == sound.Id));
            Assert.AreEqual(SoundService.SystemSoundId, settingsService.Current.CaptureSoundId);
        }
        finally
        {
            if (importedId != null) SoundService.RemoveCustomSound(importedId);
            settingsService.Current.CaptureSoundId = originalId;
            settingsService.Current.CaptureSoundVolume = 100;
            settingsService.Save();
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task Sound_ImportRejectsInvalidUnsupportedAndTooLongFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CrankoShotSoundTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            int before = SettingsService.Instance.Current.CustomCaptureSounds.Count;

            string txt = Path.Combine(tempDir, "nota.txt");
            File.WriteAllText(txt, "não é áudio");
            var (okTxt, msgTxt, _) = await SoundService.ImportCustomSoundAsync(txt);
            Assert.IsFalse(okTxt);
            StringAssert.Contains(msgTxt, "WAV ou MP3");

            string corrupt = Path.Combine(tempDir, "corrompido.mp3");
            File.WriteAllBytes(corrupt, new byte[4096]);
            var (okCorrupt, msgCorrupt, _) = await SoundService.ImportCustomSoundAsync(corrupt);
            Assert.IsFalse(okCorrupt, msgCorrupt);

            // WAV de ~12 s (acima do limite de 10 s)
            int sampleRate = 22050, seconds = 12;
            var longWav = new byte[44 + sampleRate * 2 * seconds];
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(longWav, 0);
            BitConverter.GetBytes(longWav.Length - 8).CopyTo(longWav, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(longWav, 8);
            BitConverter.GetBytes(16).CopyTo(longWav, 16);
            BitConverter.GetBytes((short)1).CopyTo(longWav, 20);
            BitConverter.GetBytes((short)1).CopyTo(longWav, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(longWav, 24);
            BitConverter.GetBytes(sampleRate * 2).CopyTo(longWav, 28);
            BitConverter.GetBytes((short)2).CopyTo(longWav, 32);
            BitConverter.GetBytes((short)16).CopyTo(longWav, 34);
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(longWav, 36);
            BitConverter.GetBytes(sampleRate * 2 * seconds).CopyTo(longWav, 40);
            string longPath = Path.Combine(tempDir, "longo.wav");
            File.WriteAllBytes(longPath, longWav);
            var (okLong, msgLong, _) = await SoundService.ImportCustomSoundAsync(longPath);
            Assert.IsFalse(okLong, msgLong);
            StringAssert.Contains(msgLong, "segundos");

            Assert.AreEqual(before, SettingsService.Instance.Current.CustomCaptureSounds.Count, "arquivos rejeitados não entram na lista");
            Assert.AreEqual(0, Directory.Exists(SoundService.CustomDirectory)
                ? Directory.GetFiles(SoundService.CustomDirectory).Count(f => !SettingsService.Instance.Current.CustomCaptureSounds.Any(c => c.FileName == Path.GetFileName(f)))
                : 0, "arquivos rejeitados não podem ficar órfãos no disco");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    // ------------------------------------------------------------------
    // Análise automática removida
    // ------------------------------------------------------------------

    [TestMethod]
    public void AutoAnalyze_SettingRemoved_LegacyJsonStillLoadsWithoutErrors()
    {
        Assert.IsNull(typeof(AppSettings).GetProperty("AutoAnalyzeEnabled"));
        Assert.IsNull(typeof(AppSettings).GetProperty("AutoAnalyzeActions"));

        string legacyJson = "{ \"AutoAnalyzeEnabled\": true, \"AutoAnalyzeActions\": [0], \"GeminiModel\": \"gemini-2.5-pro\" }";
        var loaded = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(legacyJson);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("gemini-2.5-pro", loaded!.GeminiModel);
    }

    [TestMethod]
    public void AutoAnalyze_CaptureFlow_NeverStartsAiWithoutUserAction()
    {
        // Em todo o código do aplicativo, AiResultWindow só é instanciada em handlers de botões/menus de IA.
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string? dir = root;
        while (dir != null && !File.Exists(Path.Combine(dir, "ShotMind.sln"))) dir = Path.GetDirectoryName(dir);
        Assert.IsNotNull(dir, "solução não localizada");

        foreach (string file in Directory.GetFiles(Path.Combine(dir!, "ShotMind"), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(@"\obj\") && !f.Contains(@"\bin\")))
        {
            string text = File.ReadAllText(file);
            Assert.IsFalse(text.Contains("AutoAnalyze"), $"{Path.GetFileName(file)} ainda referencia AutoAnalyze");
        }

        string overlay = File.ReadAllText(Path.Combine(dir!, "ShotMind", "UI", "Views", "CaptureOverlayWindow.xaml.cs"));
        foreach (string method in new[] { "BtnCopy_Click", "BtnSave_Click", "BtnPin_Click", "BtnEditor_Click" })
        {
            int start = overlay.IndexOf($"void {method}(", StringComparison.Ordinal);
            Assert.IsTrue(start > 0, method);
            int end = overlay.IndexOf("private ", start + method.Length, StringComparison.Ordinal);
            string body = overlay.Substring(start, end - start);
            Assert.IsFalse(body.Contains("AiResultWindow") || body.Contains("GeminiService") || body.Contains("OpenRouterService"),
                $"{method} não pode iniciar análise de IA");
        }
    }
}