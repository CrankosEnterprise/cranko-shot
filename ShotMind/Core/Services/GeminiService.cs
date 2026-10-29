using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ShotMind.Core.Models;

namespace ShotMind.Core.Services;

public class GeminiService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    private static GeminiService? _instance;
    public static GeminiService Instance => _instance ??= new GeminiService();

    private GeminiService()
    {
    }

    public static readonly List<GeminiModelInfo> ApprovedModels = new()
    {
        new("gemini-3.5-flash", "Gemini 3.5 Flash — Versão Flash compacta", "Modelo principal de alto desempenho e velocidade otimizada para capturas e tarefas cotidianas."),
        new("gemini-2.5-flash", "Gemini 2.5 Flash — Rápido e equilibrado", "Excelente velocidade com baixo consumo de cota e alta precisão multimodal."),
        new("gemini-2.5-pro", "Gemini 2.5 Pro — Análises complexas", "Modelo avançado para diagnósticos profundos, código-fonte e análises detalhadas."),
        new("gemini-2.0-flash", "Gemini 2.0 Flash — Alta velocidade", "Modelo veloz projetado para respostas ágeis em tempo real.")
    };

    public static string NormalizeModelName(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return "gemini-3.5-flash";
        string clean = modelName.Trim();
        if (clean.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring("models/".Length);
        }
        return clean;
    }

    public async Task<(bool success, List<GeminiModelInfo> models, string errorMessage)> GetAvailableModelsAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, new List<GeminiModelInfo>(ApprovedModels), "Chave da API do Gemini não configurada.");
        }

        try
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

            var response = await HttpClient.SendAsync(req);
            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var (_, err) = ParseHttpError(response.StatusCode, responseJson, apiKey: apiKey);
                return (false, new List<GeminiModelInfo>(ApprovedModels), err);
            }

            var jsonNode = JsonNode.Parse(responseJson);
            var modelsArray = jsonNode?["models"]?.AsArray();
            var discoveredModels = new List<GeminiModelInfo>();

            if (modelsArray != null)
            {
                var availableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in modelsArray)
                {
                    string rawName = m?["name"]?.GetValue<string>() ?? string.Empty;
                    string id = NormalizeModelName(rawName);

                    // Check if generateContent is supported
                    bool supportsGenerate = false;
                    var methods = m?["supportedGenerationMethods"]?.AsArray();
                    if (methods != null)
                    {
                        foreach (var meth in methods)
                        {
                            if (string.Equals(meth?.GetValue<string>(), "generateContent", StringComparison.OrdinalIgnoreCase))
                            {
                                supportsGenerate = true;
                                break;
                            }
                        }
                    }

                    if (supportsGenerate && !string.IsNullOrEmpty(id))
                    {
                        availableIds.Add(id);
                    }
                }

                // Match with ApprovedModels
                foreach (var appModel in ApprovedModels)
                {
                    if (availableIds.Contains(appModel.Id))
                    {
                        discoveredModels.Add(appModel);
                    }
                }

                // If user's preferred gemini-3.5-flash is not explicitly returned in the public list, retain it as option
                if (!discoveredModels.Any(m => string.Equals(m.Id, "gemini-3.5-flash", StringComparison.OrdinalIgnoreCase)))
                {
                    discoveredModels.Insert(0, ApprovedModels[0]);
                }

                if (discoveredModels.Count > 0)
                {
                    return (true, discoveredModels, string.Empty);
                }
            }

            return (true, new List<GeminiModelInfo>(ApprovedModels), string.Empty);
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Falha de rede ao listar modelos do Gemini.", ex);
            return (false, new List<GeminiModelInfo>(ApprovedModels), "Falha de conexão com a internet ao listar modelos disponíveis.");
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao listar modelos do Gemini.", ex);
            return (false, new List<GeminiModelInfo>(ApprovedModels), $"Erro ao consultar modelos da API: {ex.Message}");
        }
    }

    public async Task<(bool success, string message)> TestConnectionAsync(string apiKey, string modelName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "Chave da API do Gemini não configurada.");
        }

        string cleanModel = NormalizeModelName(modelName);

        try
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{cleanModel}:generateContent?key={apiKey}";
            
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = "Responda apenas com a palavra: OK" }
                        }
                    }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(req);
            string responseJson = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(responseJson);
                var candidates = jsonNode?["candidates"]?.AsArray();
                if (candidates != null && candidates.Count > 0)
                {
                    return (true, $"Conexão bem-sucedida! O modelo '{cleanModel}' foi validado e respondeu perfeitamente.");
                }
                return (true, $"Conexão validada com sucesso com o modelo '{cleanModel}'.");
            }

            return ParseHttpError(response.StatusCode, responseJson, cleanModel, apiKey);
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Falha de rede ao testar conexão com o Gemini.", ex);
            return (false, "Falha de conexão com a internet ou servidores do Google (generativelanguage.googleapis.com) inacessíveis.");
        }
        catch (TaskCanceledException)
        {
            return (false, "Tempo limite esgotado: O servidor do Google Gemini demorou mais de 45 segundos para responder.");
        }
        catch (Exception ex)
        {
            Logger.Error("Erro inesperado ao testar conexão com o Gemini.", ex);
            return (false, $"Erro inesperado: {ex.Message}");
        }
    }

    public async Task<AiResult> ExecuteActionAsync(AiActionType action, Bitmap bitmap, string? customQuestion = null, CancellationToken ct = default)
    {
        string apiKey = SettingsService.Instance.GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiResult
            {
                ActionType = action,
                IsSuccess = false,
                ErrorMessage = "Chave da API do Gemini não configurada. Abra as Configurações (ícone de engrenagem) e informe sua chave."
            };
        }

        string model = SettingsService.Instance.Current.GeminiModel;
        string prompt = BuildPromptForAction(action, customQuestion);
        string title = GetActionTitle(action);
        bool useSearchGrounding = action == AiActionType.SearchWeb;

        return await CallGeminiAsync(action, title, prompt, bitmap, apiKey, model, useSearchGrounding, ct);
    }

    private string BuildPromptForAction(AiActionType action, string? customQuestion) => BuildPromptForActionStatic(action, customQuestion);

    public static string BuildPromptForActionStatic(AiActionType action, string? customQuestion)
    {
        return action switch
        {
            AiActionType.Analyze =>
                "Analise detalhadamente a imagem capturada da tela. Explique em português do Brasil o que está visível, identificando a interface, conteúdos principais e o contexto.",

            AiActionType.ExtractText =>
                "Extraia e organize todo o texto legível nesta imagem. Preserve a hierarquia, tabelas, código-fonte ou listas exatamente como aparecem.",

            AiActionType.Translate =>
                "Identifique os textos nesta imagem e forneça a tradução completa e precisa para o Português do Brasil. Mantenha os termos técnicos compreensíveis.",

            AiActionType.Summarize =>
                "Crie um resumo conciso e estruturado em tópicos dos pontos essenciais e informações contidas nesta captura de tela.",

            AiActionType.ExplainError =>
                "Identifique qualquer erro, código de status, alerta ou bug visível nesta captura. Explique a causa provável e proponha passos claros e práticos para solucionar o problema.",

            AiActionType.AskQuestion =>
                string.IsNullOrWhiteSpace(customQuestion)
                    ? "Responda à dúvida sobre o conteúdo exibido nesta imagem."
                    : $"Com base nesta captura de tela, responda com precisão à seguinte pergunta do usuário: {customQuestion}",

            AiActionType.IdentifyElements =>
                "Identifique e liste todos os componentes, controles de interface, janelas, botões, ícones ou elementos gráficos presentes na imagem.",

            AiActionType.SearchWeb =>
                string.IsNullOrWhiteSpace(customQuestion)
                    ? "Pesquise na internet sobre os tópicos, erros, produtos ou conteúdos identificados nesta imagem e sintetize informações atualizadas com fontes."
                    : $"Pesquise informações atualizadas na web com base nesta imagem e responda à pergunta: {customQuestion}",

            _ => "Analise o conteúdo visual desta imagem."
        };
    }

    public static object BuildChatPayload(
        IReadOnlyList<AiChatMessage> history,
        string newUserMessage,
        string? base64ImageJpeg = null)
    {
        var contentsList = new List<object>();
        bool hasUserHistory = false;

        foreach (var msg in history)
        {
            if (msg.IsError) continue;

            if (msg.IsUser)
            {
                var parts = new List<object>
                {
                    new { text = msg.Text }
                };

                // Attach image only to the first user turn if available
                if (!hasUserHistory && !string.IsNullOrEmpty(base64ImageJpeg))
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = "image/jpeg",
                            data = base64ImageJpeg
                        }
                    });
                }

                contentsList.Add(new
                {
                    role = "user",
                    parts = parts.ToArray()
                });
                hasUserHistory = true;
            }
            else if (msg.IsModel)
            {
                contentsList.Add(new
                {
                    role = "model",
                    parts = new object[]
                    {
                        new { text = msg.Text }
                    }
                });
            }
        }

        // Now add the new user turn
        var newParts = new List<object>
        {
            new { text = newUserMessage }
        };

        // If no user history has had the image attached, attach it now
        if (!hasUserHistory && !string.IsNullOrEmpty(base64ImageJpeg))
        {
            newParts.Add(new
            {
                inline_data = new
                {
                    mime_type = "image/jpeg",
                    data = base64ImageJpeg
                }
            });
        }

        contentsList.Add(new
        {
            role = "user",
            parts = newParts.ToArray()
        });

        return new { contents = contentsList };
    }

    public async Task<(bool success, string responseText, string errorMessage)> SendChatMessageAsync(
        IReadOnlyList<AiChatMessage> history,
        string newUserMessage,
        Bitmap? imageContext,
        CancellationToken ct = default)
    {
        string apiKey = SettingsService.Instance.GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, string.Empty, "Chave da API do Google Gemini não configurada. Abra as Configurações e informe sua chave.");
        }

        string model = SettingsService.Instance.Current.GeminiModel;
        string cleanModel = NormalizeModelName(model);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{cleanModel}:generateContent?key={apiKey}";

        try
        {
            string? base64Image = null;
            if (imageContext != null)
            {
                byte[] jpgBytes = ScreenCaptureService.ToJpgByteArray(imageContext, 90);
                base64Image = Convert.ToBase64String(jpgBytes);
            }

            var payload = BuildChatPayload(history, newUserMessage, base64Image);
            string jsonBody = JsonSerializer.Serialize(payload);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(req, ct);
            string responseJson = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var (_, errorMsg) = ParseHttpError(response.StatusCode, responseJson, cleanModel, apiKey);
                return (false, string.Empty, errorMsg);
            }

            var jsonNode = JsonNode.Parse(responseJson);
            var candidates = jsonNode?["candidates"]?.AsArray();
            if (candidates == null || candidates.Count == 0)
            {
                return (false, string.Empty, "O modelo não retornou respostas para esta pergunta.");
            }

            var parts = candidates[0]?["content"]?["parts"]?.AsArray();
            if (parts == null || parts.Count == 0)
            {
                string finishReason = candidates[0]?["finishReason"]?.GetValue<string>() ?? string.Empty;
                return (false, string.Empty, $"A resposta foi bloqueada pelos filtros de segurança do modelo (Motivo: {finishReason}).");
            }

            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                string text = p?["text"]?.GetValue<string>() ?? string.Empty;
                sb.Append(text);
            }

            string answer = sb.ToString().Trim();
            return (true, answer, string.Empty);
        }
        catch (OperationCanceledException)
        {
            return (false, string.Empty, "Operação cancelada pelo usuário.");
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Falha de rede no chat com o Gemini.", ex);
            return (false, string.Empty, "Falha de conexão com a internet ou servidores do Google indisponíveis.");
        }
        catch (Exception ex)
        {
            Logger.LogException("GeminiService", "SendChatMessageAsync", ex);
            return (false, string.Empty, $"Erro durante a comunicação com o Gemini: {ex.Message}");
        }
    }

    public static string GetActionTitle(AiActionType action)
    {
        return action switch
        {
            AiActionType.Analyze => "Análise da Imagem",
            AiActionType.ExtractText => "Extração Inteligente de Texto",
            AiActionType.Translate => "Tradução para Português",
            AiActionType.Summarize => "Resumo da Captura",
            AiActionType.ExplainError => "Diagnóstico e Solução de Erro",
            AiActionType.AskQuestion => "Perguntar à IA",
            AiActionType.IdentifyElements => "Identificação de Elementos",
            AiActionType.SearchWeb => "Pesquisa na Web com Gemini",
            _ => "Modo Analista"
        };
    }

    private async Task<AiResult> CallGeminiAsync(
        AiActionType action,
        string title,
        string prompt,
        Bitmap bitmap,
        string apiKey,
        string model,
        bool useSearchGrounding,
        CancellationToken ct)
    {
        var result = new AiResult
        {
            ActionType = action,
            ActionTitle = title,
            Prompt = prompt,
            Timestamp = DateTime.Now
        };

        try
        {
            byte[] jpgBytes = ScreenCaptureService.ToJpgByteArray(bitmap, 90);
            string base64Image = Convert.ToBase64String(jpgBytes);

            string cleanModel = NormalizeModelName(model);
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{cleanModel}:generateContent?key={apiKey}";

            var parts = new List<object>
            {
                new { text = prompt },
                new
                {
                    inline_data = new
                    {
                        mime_type = "image/jpeg",
                        data = base64Image
                    }
                }
            };

            var payloadDict = new Dictionary<string, object>
            {
                ["contents"] = new[]
                {
                    new { parts = parts.ToArray() }
                }
            };

            if (useSearchGrounding)
            {
                payloadDict["tools"] = new object[]
                {
                    new { google_search = new { } }
                };
            }

            string jsonBody = JsonSerializer.Serialize(payloadDict);
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(req, ct);
            string responseJson = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var (success, errorMsg) = ParseHttpError(response.StatusCode, responseJson, cleanModel, apiKey);
                result.IsSuccess = false;
                result.ErrorMessage = errorMsg;
                return result;
            }

            // Parse response
            var jsonNode = JsonNode.Parse(responseJson);
            var candidates = jsonNode?["candidates"]?.AsArray();
            if (candidates == null || candidates.Count == 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "A IA não retornou conteúdo para esta captura.";
                return result;
            }

            var firstCandidate = candidates[0];
            var partsArray = firstCandidate?["content"]?["parts"]?.AsArray();
            var sbText = new StringBuilder();

            if (partsArray != null)
            {
                foreach (var p in partsArray)
                {
                    string? t = p?["text"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(t))
                    {
                        sbText.Append(t);
                    }
                }
            }

            result.ResponseText = sbText.ToString().Trim();
            if (string.IsNullOrEmpty(result.ResponseText))
            {
                result.ResponseText = "(Resposta sem texto retornado pelo modelo)";
            }

            // Extract real web grounding sources if available
            var groundingMetadata = firstCandidate?["groundingMetadata"];
            if (groundingMetadata != null)
            {
                var groundingChunks = groundingMetadata["groundingChunks"]?.AsArray();
                if (groundingChunks != null)
                {
                    foreach (var chunk in groundingChunks)
                    {
                        var web = chunk?["web"];
                        if (web != null)
                        {
                            string? uri = web["uri"]?.GetValue<string>();
                            string? srcTitle = web["title"]?.GetValue<string>() ?? uri;

                            if (!string.IsNullOrEmpty(uri))
                            {
                                result.Sources.Add(new WebSource
                                {
                                    Title = srcTitle ?? uri,
                                    Url = uri
                                });
                            }
                        }
                    }
                }
            }

            result.IsSuccess = true;
            Logger.Info($"Ação IA '{action}' concluída com sucesso com o modelo {model}.");
            return result;
        }
        catch (HttpRequestException ex)
        {
            Logger.Error($"Falha de rede ao executar ação IA '{action}'.", ex);
            result.IsSuccess = false;
            result.ErrorMessage = "Erro de conexão com a internet. Verifique sua rede e tente novamente.";
            return result;
        }
        catch (TaskCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "A requisição da IA foi cancelada ou excedeu o tempo limite.";
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"Erro inesperado na ação IA '{action}'.", ex);
            result.IsSuccess = false;
            result.ErrorMessage = $"Erro inesperado: {ex.Message}";
            return result;
        }
    }

    public static (bool success, string message) ParseHttpError(
        System.Net.HttpStatusCode statusCode,
        string responseJson,
        string? modelName = null,
        string? apiKey = null)
    {
        string detail = string.Empty;
        try
        {
            var node = JsonNode.Parse(responseJson);
            detail = node?["error"]?["message"]?.GetValue<string>() ?? string.Empty;
        }
        catch { }

        // Sanitize API key from detail string to prevent exposure in logs or UI
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            detail = detail.Replace(apiKey, "[CHAVE_PROTEGIDA]");
        }

        string message = statusCode switch
        {
            System.Net.HttpStatusCode.BadRequest =>
                $"Requisição inválida (400):\n{(string.IsNullOrWhiteSpace(detail) ? "Verifique os parâmetros informados." : detail)}\n\nVerifique se o modelo selecionado suporta o formato da solicitação.",

            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                detail.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase) || detail.Contains("API key not valid", StringComparison.OrdinalIgnoreCase)
                    ? "Chave da API inválida (403): O Google recusou a chave de autenticação informada. Verifique se a chave foi copiada corretamente do Google AI Studio (aistudio.google.com)."
                    : detail.Contains("SERVICE_DISABLED", StringComparison.OrdinalIgnoreCase) || detail.Contains("has not been used in project", StringComparison.OrdinalIgnoreCase)
                    ? $"Serviço desativado (403): A Generative Language API não está ativada no projeto Google Cloud associado a esta chave.\n\nDetalhes: {detail}"
                    : detail.Contains("QUOTA_EXCEEDED", StringComparison.OrdinalIgnoreCase) || detail.Contains("Resource has been exhausted", StringComparison.OrdinalIgnoreCase)
                    ? $"Cota esgotada (403/429): O limite de requisições gratuitas foi atingido para este projeto/chave no Google AI Studio.\n\nDetalhes: {detail}"
                    : detail.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase)
                    ? $"Permissão negada (403): Sua chave de API não possui autorização para este modelo ou recurso.\n\nDetalhes: {detail}"
                    : $"Acesso negado ou restrição de conta (403):\n{(string.IsNullOrWhiteSpace(detail) ? "Verifique sua chave e permissões no Google AI Studio." : detail)}",

            System.Net.HttpStatusCode.NotFound =>
                !string.IsNullOrWhiteSpace(modelName)
                    ? $"Modelo inexistente ou indisponível (404):\nO modelo '{modelName}' não foi encontrado ou não é compatível com o método 'generateContent' na API v1beta do Google Gemini.\n\nDetalhes da API: {(string.IsNullOrWhiteSpace(detail) ? "Recurso não localizado (NOT_FOUND)" : detail)}\n\nDiagnóstico: Este identificador não está habilitado para o seu projeto Google ou foi descontinuado pelo Google. Recomendamos selecionar 'gemini-2.5-flash' ou 'gemini-2.0-flash' nas configurações."
                    : $"Recurso do Gemini não encontrado (404):\n{detail}",

            System.Net.HttpStatusCode.TooManyRequests =>
                $"Limite de requisições ou cota excedida (429 - Resource Exhausted):\n{(string.IsNullOrWhiteSpace(detail) ? "A taxa de chamadas por minuto/dia foi atingida." : detail)}\n\nAguarde alguns instantes antes de tentar novamente ou verifique suas cotas no Google AI Studio.",

            System.Net.HttpStatusCode.InternalServerError or System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.BadGateway or System.Net.HttpStatusCode.GatewayTimeout =>
                $"Instabilidade temporária nos servidores do Google Gemini ({(int)statusCode}):\n{(string.IsNullOrWhiteSpace(detail) ? "Os servidores do Google estão temporariamente indisponíveis." : detail)}\n\nO problema é externo e temporário. Tente novamente em alguns minutos.",

            _ =>
                $"Erro retornado pela API do Gemini ({(int)statusCode}):\n{detail}"
        };

        return (false, message);
    }
}

public class GeminiModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public GeminiModelInfo() { }

    public GeminiModelInfo(string id, string displayName, string description)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
    }

    public override string ToString() => DisplayName;
}
