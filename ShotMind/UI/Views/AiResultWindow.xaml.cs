using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using ShotMind.Core;
using ShotMind.Core.Models;
using ShotMind.Core.Services;

namespace ShotMind.UI.Views;

public partial class AiResultWindow : Window
{
    private Bitmap? _sourceBitmap;
    private CancellationTokenSource? _cts;
    private bool _isClosing = false;
    private bool _isProcessing = false;

    public ObservableCollection<AiChatMessage> Messages { get; } = new();

    public AiResultWindow()
    {
        InitializeComponent();
        ChatItemsControl.ItemsSource = Messages;
    }

    private static string GetActiveProviderAndModelSubtitle(int width, int height, bool forceGemini = false)
    {
        var s = SettingsService.Instance.Current;
        if (!forceGemini && s.AiProvider == AiProviderType.OpenRouter)
        {
            return $"OpenRouter ({s.OpenRouterModel}) • {width}×{height} px";
        }
        return $"Google Gemini ({s.GeminiModel}) • {width}×{height} px";
    }

    private static string GetActiveProviderLabel(bool forceGemini = false)
    {
        var s = SettingsService.Instance.Current;
        if (!forceGemini && s.AiProvider == AiProviderType.OpenRouter)
        {
            return "✦ OpenRouter";
        }
        return "✦ Gemini";
    }

    public void StartChatMode(Bitmap bitmap, string? initialQuestion = null)
    {
        _sourceBitmap?.Dispose();
        _sourceBitmap = new Bitmap(bitmap);

        ActionTitleText.Text = "Perguntar à IA";
        SubtitleText.Text = GetActiveProviderAndModelSubtitle(_sourceBitmap.Width, _sourceBitmap.Height);
        TxtContextInfo.Text = $"Captura associada como contexto visual ({_sourceBitmap.Width}×{_sourceBitmap.Height} px).";

        SetStatusBadge("PRONTO", "#10B981");
        ProcessingIndicator.Visibility = Visibility.Collapsed;
        BtnCancelRequest.Visibility = Visibility.Collapsed;
        BtnSendMessage.IsEnabled = true;

        if (Messages.Count == 0)
        {
            // Initial welcoming system message
            Messages.Add(new AiChatMessage
            {
                Role = "model",
                ProviderLabel = GetActiveProviderLabel(),
                Text = "Como posso ajudar com esta captura de tela?"
            });
        }

        if (!string.IsNullOrWhiteSpace(initialQuestion))
        {
            QuestionInput.Text = initialQuestion;
            _ = SubmitUserMessageAsync();
        }
        else
        {
            FocusInput();
        }
    }

    public void PrepareQuestionMode(Bitmap bitmap)
    {
        StartChatMode(bitmap);
    }

    public async Task ExecuteOcrAsync(Bitmap bitmap)
    {
        if (_isProcessing) return;

        _sourceBitmap?.Dispose();
        _sourceBitmap = new Bitmap(bitmap);

        ActionTitleText.Text = "OCR Local — Reconhecimento Óptico de Texto";
        SubtitleText.Text = "Processado localmente no dispositivo pelo Windows.Media.Ocr (100% offline)";
        TxtContextInfo.Text = "Texto extraído localmente da captura. Você pode continuar conversando e fazendo perguntas sobre o texto abaixo com a IA.";

        SetStatusBadge("PROCESSANDO OCR...", "#8B5CF6");
        ProcessingIndicator.Visibility = Visibility.Visible;
        BtnSendMessage.IsEnabled = false;

        try
        {
            var result = await OcrService.RecognizeTextAsync(_sourceBitmap);
            if (_isClosing) return;

            if (result.Success)
            {
                SetStatusBadge("CONCLUÍDO", "#10B981");
                Messages.Add(new AiChatMessage
                {
                    Role = "model",
                    ProviderLabel = "🔍 OCR Local",
                    Text = $"📄 **Texto extraído via OCR local ({result.LanguageUsed}):**\n\n{result.Text}"
                });
            }
            else
            {
                SetStatusBadge("FALHA OCR", "#EF4444");
                Messages.Add(new AiChatMessage
                {
                    Role = "system_error",
                    Text = $"Não foi possível realizar o OCR local: {result.ErrorMessage}"
                });
            }
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                SetStatusBadge("ERRO", "#EF4444");
                Messages.Add(new AiChatMessage
                {
                    Role = "system_error",
                    Text = $"Erro durante OCR: {ex.Message}"
                });
                Logger.LogException("AiResultWindow", "ExecuteOcrAsync", ex);
            }
        }
        finally
        {
            ProcessingIndicator.Visibility = Visibility.Collapsed;
            BtnSendMessage.IsEnabled = true;
            ScrollChatToEnd();
            FocusInput();
        }
    }

    public async Task ExecuteGeminiActionAsync(AiActionType action, Bitmap bitmap, string? customQuestion = null)
    {
        if (_isProcessing) return;

        _sourceBitmap?.Dispose();
        _sourceBitmap = new Bitmap(bitmap);

        bool isWebSearch = action == AiActionType.SearchWeb;
        ActionTitleText.Text = GeminiService.GetActionTitle(action);
        SubtitleText.Text = GetActiveProviderAndModelSubtitle(_sourceBitmap.Width, _sourceBitmap.Height, forceGemini: isWebSearch);

        string initialPrompt = customQuestion ?? action switch
        {
            AiActionType.Analyze => "Analise detalhadamente esta imagem capturada da tela e explique seu conteúdo principal.",
            AiActionType.ExtractText => "Extraia e estruture todo o texto legível nesta captura de tela.",
            AiActionType.Translate => "Identifique os textos nesta captura e forneça uma tradução clara e precisa para o Português do Brasil.",
            AiActionType.Summarize => "Faça um resumo executivo dos pontos principais apresentados nesta captura.",
            AiActionType.ExplainError => "Identifique qualquer erro ou aviso visível nesta captura e explique como solucioná-lo.",
            AiActionType.IdentifyElements => "Identifique todos os controles de interface e elementos visuais visíveis na imagem.",
            AiActionType.SearchWeb => "Pesquise na internet sobre os tópicos visíveis nesta imagem e apresente informações atualizadas.",
            _ => "Analise o conteúdo visual desta imagem."
        };

        // Pesquisa na Web utiliza exclusivamente a implementação original de Grounding do Google Gemini
        if (isWebSearch)
        {
            await ExecuteWebSearchActionAsync(initialPrompt, customQuestion);
            return;
        }

        QuestionInput.Text = initialPrompt;
        await SubmitUserMessageAsync();
    }

    private async Task ExecuteWebSearchActionAsync(string displayPrompt, string? customQuestion)
    {
        if (_isProcessing || _sourceBitmap == null) return;
        _isProcessing = true;

        Messages.Add(new AiChatMessage
        {
            Role = "user",
            Text = displayPrompt,
            Timestamp = DateTime.Now
        });
        ScrollChatToEnd();

        SetStatusBadge("PESQUISANDO NA WEB...", "#8B5CF6");
        if (TxtProcessingStatus != null)
        {
            TxtProcessingStatus.Text = "O Google Gemini está pesquisando na web e analisando a captura...";
        }
        ProcessingIndicator.Visibility = Visibility.Visible;
        BtnCancelRequest.Visibility = Visibility.Visible;
        BtnSendMessage.IsEnabled = false;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            var result = await GeminiService.Instance.ExecuteActionAsync(AiActionType.SearchWeb, _sourceBitmap, customQuestion, _cts.Token);
            if (_isClosing) return;

            if (result.IsSuccess)
            {
                SetStatusBadge("CONCLUÍDO", "#10B981");
                Messages.Add(new AiChatMessage
                {
                    Role = "model",
                    ProviderLabel = "✦ Gemini (Pesquisa Web)",
                    Text = result.ResponseText,
                    Timestamp = DateTime.Now
                });

                if (result.Sources.Count > 0 && SourcesSection != null && SourcesList != null)
                {
                    SourcesList.ItemsSource = result.Sources;
                    SourcesSection.Visibility = Visibility.Visible;
                }
            }
            else
            {
                SetStatusBadge("FALHA", "#EF4444");
                Messages.Add(new AiChatMessage
                {
                    Role = "system_error",
                    Text = result.ErrorMessage ?? "Não foi possível concluir a pesquisa na web.",
                    Timestamp = DateTime.Now
                });
            }
        }
        catch (OperationCanceledException)
        {
            if (!_isClosing)
            {
                SetStatusBadge("CANCELADO", "#F59E0B");
                Messages.Add(new AiChatMessage
                {
                    Role = "system_error",
                    Text = "Pesquisa na web interrompida pelo usuário."
                });
            }
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                SetStatusBadge("ERRO", "#EF4444");
                Messages.Add(new AiChatMessage
                {
                    Role = "system_error",
                    Text = $"Erro inesperado: {ex.Message}"
                });
                Logger.LogException("AiResultWindow", "ExecuteWebSearchActionAsync", ex);
            }
        }
        finally
        {
            _isProcessing = false;
            if (!_isClosing)
            {
                ProcessingIndicator.Visibility = Visibility.Collapsed;
                BtnCancelRequest.Visibility = Visibility.Collapsed;
                BtnSendMessage.IsEnabled = true;
                ScrollChatToEnd();
                FocusInput();
            }
        }
    }

    private async Task SubmitUserMessageAsync()
    {
        if (_isProcessing || _sourceBitmap == null) return;

        string question = QuestionInput.Text.Trim();
        if (string.IsNullOrEmpty(question)) return;

        _isProcessing = true;
        QuestionInput.Text = string.Empty;

        var activeProvider = SettingsService.Instance.Current.AiProvider;
        bool useOpenRouter = activeProvider == AiProviderType.OpenRouter;
        string providerLabel = GetActiveProviderLabel();

        // Add user message to history
        var userMsg = new AiChatMessage
        {
            Role = "user",
            Text = question,
            Timestamp = DateTime.Now
        };
        Messages.Add(userMsg);
        ScrollChatToEnd();

        // UI state: processing
        SetStatusBadge("GERANDO RESPOSTA...", "#8B5CF6");
        if (TxtProcessingStatus != null)
        {
            TxtProcessingStatus.Text = useOpenRouter
                ? "O OpenRouter está analisando o contexto e gerando a resposta..."
                : "O Gemini está analisando o contexto e gerando a resposta...";
        }
        ProcessingIndicator.Visibility = Visibility.Visible;
        BtnCancelRequest.Visibility = Visibility.Visible;
        BtnSendMessage.IsEnabled = false;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            // Snapshot current history excluding the newly added user message
            var historySnapshot = new Collection<AiChatMessage>();
            for (int i = 0; i < Messages.Count - 1; i++)
            {
                historySnapshot.Add(Messages[i]);
            }

            (bool success, string responseText, string errorMsg) = useOpenRouter
                ? await OpenRouterService.Instance.SendChatMessageAsync(
                    historySnapshot,
                    question,
                    _sourceBitmap,
                    _cts.Token)
                : await GeminiService.Instance.SendChatMessageAsync(
                    historySnapshot,
                    question,
                    _sourceBitmap,
                    _cts.Token);

            if (_isClosing) return;

            if (success)
            {
                SetStatusBadge("CONCLUÍDO", "#10B981");
                Messages.Add(new AiChatMessage
                {
                    Role = "model",
                    ProviderLabel = providerLabel,
                    Text = responseText,
                    Timestamp = DateTime.Now
                });
            }
            else
            {
                SetStatusBadge("FALHA", "#EF4444");
                Messages.Add(new AiChatMessage
                {
                    Role = "system_error",
                    Text = errorMsg,
                    Timestamp = DateTime.Now
                });
            }
        }
        catch (OperationCanceledException)
        {
            if (!_isClosing)
            {
                SetStatusBadge("CANCELADO", "#F59E0B");
                Messages.Add(new AiChatMessage
                {
                    Role = "system_error",
                    Text = "Geração de resposta interrompida pelo usuário."
                });
            }
        }
        catch (Exception ex)
        {
            if (!_isClosing)
            {
                SetStatusBadge("ERRO", "#EF4444");
                Messages.Add(new AiChatMessage
                {
                    Role = "system_error",
                    Text = $"Erro inesperado: {ex.Message}"
                });
                Logger.LogException("AiResultWindow", "SubmitUserMessageAsync", ex);
            }
        }
        finally
        {
            _isProcessing = false;
            if (!_isClosing)
            {
                ProcessingIndicator.Visibility = Visibility.Collapsed;
                BtnCancelRequest.Visibility = Visibility.Collapsed;
                BtnSendMessage.IsEnabled = true;
                ScrollChatToEnd();
                FocusInput();
            }
        }
    }

    private void QuestionInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Shift + Enter inserts new line
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                return;
            }

            // Normal Enter sends the message
            e.Handled = true;
            _ = SubmitUserMessageAsync();
        }
    }

    private void BtnSendMessage_Click(object sender, RoutedEventArgs e)
    {
        _ = SubmitUserMessageAsync();
    }

    private void BtnCancelRequest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _cts?.Cancel();
        }
        catch { }
    }

    private void BtnClearChat_Click(object sender, RoutedEventArgs e)
    {
        if (Messages.Count == 0) return;

        var result = MessageBox.Show(
            "Deseja realmente limpar todo o histórico da conversa atual?",
            "Limpar Conversa",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Messages.Clear();
            if (SourcesSection != null) SourcesSection.Visibility = Visibility.Collapsed;
            Messages.Add(new AiChatMessage
            {
                Role = "model",
                ProviderLabel = GetActiveProviderLabel(),
                Text = "Conversa reiniciada. A captura de tela original continua associada como contexto. Como posso ajudar?"
            });
            ScrollChatToEnd();
            FocusInput();
        }
    }

    private void BtnCopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string text && !string.IsNullOrEmpty(text))
        {
            try
            {
                Clipboard.SetText(text);
                MessageBox.Show("Resposta copiada para a área de transferência!", "CrankoShot", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("Falha ao copiar texto para a área de transferência.", ex);
            }
        }
    }

    private void SetStatusBadge(string text, string hexColor)
    {
        StatusBadgeText.Text = text;
        try
        {
            var color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(hexColor);
            StatusBadgeText.Foreground = new SolidColorBrush(color);
        }
        catch { }
    }

    private void ScrollChatToEnd()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ChatScrollViewer.ScrollToEnd();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void FocusInput()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            QuestionInput.Focus();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Erro ao abrir link: {e.Uri}", ex);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _isClosing = true;
        try { _cts?.Cancel(); } catch { }
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
        try
        {
            _cts?.Cancel();
        }
        catch { }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosing = true;
        base.OnClosed(e);
        _sourceBitmap?.Dispose();
        _sourceBitmap = null;
        try
        {
            _cts?.Dispose();
            _cts = null;
        }
        catch { }
    }
}
