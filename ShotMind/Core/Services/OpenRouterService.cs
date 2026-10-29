using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ShotMind.Core.Models;

namespace ShotMind.Core.Services;

public class OpenRouterService
{
    public const string BaseApiUrl = "https://openrouter.ai/api/v1";
    public const string ChatCompletionsUrl = "https://openrouter.ai/api/v1/chat/completions";
    public const string ModelsUrl = "https://openrouter.ai/api/v1/models";
    public const string AuthKeyUrl = "https://openrouter.ai/api/v1/auth/key";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(50)
    };

    private static OpenRouterService? _instance;
    public static OpenRouterService Instance => _instance ??= new OpenRouterService();

    private List<OpenRouterModelInfo>? _cachedModels;

    private OpenRouterService()
    {
    }

    /// <summary>
    /// Modelos verificados na API oficial do OpenRouter, priorizando modelos gratuitos compatíveis com
    /// análise de imagens + retorno em texto (Visão + Texto), seguidos por modelos gratuitos de texto.
    /// Nenhum modelo de geração de imagem é incluído.
    /// </summary>
    public static readonly List<OpenRouterModelInfo> DefaultModels = new()
    {
        // 1. Modelos Gratuitos com Visão (Imagem + Texto -> Texto) - PRIORIDADE
        new(
            "google/gemma-4-31b-it:free",
            "Google: Gemma 4 31B",
            "✦ Google: Gemma 4 31B [Gratuito • Visão + Texto]",
            "Modelo multimodal gratuito de alta capacidade (262K contexto). Excelente para análise de imagens, extração de texto, tradução e diagnóstico de erros.",
            isFree: true,
            supportsVision: true,
            supportsText: true,
            contextLength: 262144),

        new(
            "google/gemma-4-26b-a4b-it:free",
            "Google: Gemma 4 26B A4B",
            "✦ Google: Gemma 4 26B A4B [Gratuito • Visão + Texto]",
            "Modelo multimodal gratuito ágil (arquitetura MoE, 262K contexto) ideal para leitura rápida de capturas de tela e respostas em texto.",
            isFree: true,
            supportsVision: true,
            supportsText: true,
            contextLength: 262144),

        new(
            "nvidia/nemotron-3.5-super-vl:free",
            "NVIDIA: Nemotron 3.5 Super VL",
            "✦ NVIDIA: Nemotron 3.5 Super VL [Gratuito • Visão + Texto]",
            "Modelo gratuito de visão e linguagem da NVIDIA (1M contexto) especializado em compreensão de interfaces, documentos e capturas.",
            isFree: true,
            supportsVision: true,
            supportsText: true,
            contextLength: 1000000),

        new(
            "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free",
            "NVIDIA: Nemotron 3 Nano Omni",
            "✦ NVIDIA: Nemotron 3 Nano Omni [Gratuito • Visão + Texto]",
            "Modelo multimodal gratuito da NVIDIA com raciocínio avançado (256K contexto) para resolução de problemas visuais e análise técnica.",
            isFree: true,
            supportsVision: true,
            supportsText: true,
            contextLength: 256000),

        new(
            "openrouter/free",
            "Free Models Router",
            "✦ OpenRouter: Roteador Automático [Gratuito • Visão + Texto]",
            "Roteador oficial do OpenRouter que seleciona automaticamente um modelo gratuito compatível com imagem e texto disponível no momento.",
            isFree: true,
            supportsVision: true,
            supportsText: true,
            contextLength: 200000),

        new(
            "thinkingmachines/inkling:free",
            "Thinking Machines: Inkling",
            "✦ Thinking Machines: Inkling [Gratuito • Visão + Texto]",
            "Modelo multimodal gratuito (1M contexto) compatível com análise de imagens e retorno estruturado em texto.",
            isFree: true,
            supportsVision: true,
            supportsText: true,
            contextLength: 1048576),

        new(
            "thinkingmachines/inkling-small:free",
            "Thinking Machines: Inkling Small",
            "✦ Thinking Machines: Inkling Small [Gratuito • Visão + Texto]",
            "Versão compacta e veloz do modelo multimodal Inkling (1M contexto) para respostas rápidas sobre capturas.",
            isFree: true,
            supportsVision: true,
            supportsText: true,
            contextLength: 1048576),

        new(
            "dots-studio/dots-3-note-preview:free",
            "Dots Studio: Dots3-Note Preview",
            "✦ Dots Studio: Dots3-Note Preview [Gratuito • Visão + Texto]",
            "Modelo multimodal gratuito (512K contexto) focado em leitura de imagens, notas estruturadas e resumos.",
            isFree: true,
            supportsVision: true,
            supportsText: true,
            contextLength: 512000),

        // 2. Modelos Gratuitos de Texto (Texto -> Texto) para tarefas que não precisam de imagem
        new(
            "nvidia/nemotron-3-ultra-550b-a55b:free",
            "NVIDIA: Nemotron 3 Ultra",
            "📝 NVIDIA: Nemotron 3 Ultra 550B [Gratuito • Somente Texto]",
            "Modelo gratuito de texto de grande escala (1M contexto) para perguntas, tradução, resumo e análise de textos/OCR.",
            isFree: true,
            supportsVision: false,
            supportsText: true,
            contextLength: 1000000),

        new(
            "nvidia/nemotron-3.5-lightning:free",
            "NVIDIA: Nemotron 3.5 Lightning",
            "📝 NVIDIA: Nemotron 3.5 Lightning [Gratuito • Somente Texto]",
            "Modelo gratuito de texto ultrarrápido (1M contexto) para conversas, resumos e explicações de texto.",
            isFree: true,
            supportsVision: false,
            supportsText: true,
            contextLength: 1000000),

        new(
            "cohere/north-mini-code:free",
            "Cohere: North Mini Code",
            "📝 Cohere: North Mini Code [Gratuito • Somente Texto]",
            "Modelo gratuito de texto (256K contexto) especializado em análise de código-fonte, logs e mensagens de erro.",
            isFree: true,
            supportsVision: false,
            supportsText: true,
            contextLength: 256000),

        new(
            "xingchen-agi/xing4.0-29b-a4b:free",
            "XingChen: Xing4.0 29B A4B",
            "📝 XingChen: Xing4.0 29B A4B [Gratuito • Somente Texto]",
            "Modelo gratuito de texto (262K contexto) para perguntas gerais, tradução e síntese textual.",
            isFree: true,
            supportsVision: false,
            supportsText: true,
            contextLength: 262144),

        new(
            "recursive/flint-v1.0:free",
            "Recursive: Flint",
            "📝 Recursive: Flint v1.0 [Gratuito • Somente Texto]",
            "Modelo gratuito de texto (262K contexto) para tarefas textuais e conversação.",
            isFree: true,
            supportsVision: false,
            supportsText: true,
            contextLength: 262144),

        new(
            "liquid/lfm-2.5-2.6b:free",
            "LiquidAI: LFM2.5-2.6B",
            "📝 LiquidAI: LFM 2.5 2.6B [Gratuito • Somente Texto]",
            "Modelo compacto e leve de texto gratuito (65K contexto) para respostas rápidas.",
            isFree: true,
            supportsVision: false,
            supportsText: true,
            contextLength: 65536)
    };

    public static string NormalizeModelName(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return "google/gemma-4-31b-it:free";
        }
        return modelName.Trim();
    }

    public OpenRouterModelInfo? GetModelMetadata(string? modelId)
    {
        string cleanId = NormalizeModelName(modelId);
        if (_cachedModels != null)
        {
            var cached = _cachedModels.FirstOrDefault(m => m.Id.Equals(cleanId, StringComparison.OrdinalIgnoreCase));
            if (cached != null) return cached;
        }
        return DefaultModels.FirstOrDefault(m => m.Id.Equals(cleanId, StringComparison.OrdinalIgnoreCase));
    }

    public bool DoesModelSupportVision(string? modelId)
    {
        var meta = GetModelMetadata(modelId);
        if (meta != null)
        {
            return meta.SupportsVision;
        }
        // Para modelos customizados não listados, assume suporte a visão a menos que falhe com fallback automático
        return true;
    }

    private static void AddOpenRouterHeaders(HttpRequestMessage req, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey.Trim()}");
        }
        req.Headers.TryAddWithoutValidation("HTTP-Referer", "https://crankoshot.app");
        req.Headers.TryAddWithoutValidation("X-Title", "CrankoShot");
    }

    /// <summary>
    /// Consulta o endpoint oficial de modelos do OpenRouter (https://openrouter.ai/api/v1/models),
    /// verificando modalidades de entrada/saída, preços e disponibilidade antes de classificá-los.
    /// Exclui modelos de geração de imagem ou áudio.
    /// </summary>
    public async Task<(bool success, List<OpenRouterModelInfo> models, string message)> GetAvailableModelsAsync(bool freeOnly = true, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ModelsUrl);
            AddOpenRouterHeaders(req, null);

            var response = await HttpClient.SendAsync(req, ct);
            string responseJson = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var fallback = GetFilteredDefaults(freeOnly);
                return (false, fallback, $"Não foi possível atualizar a lista online ({(int)response.StatusCode}). Exibindo modelos verificados locais.");
            }

            var parsed = FilterAndParseModelsJson(responseJson, freeOnly);
            if (parsed.Count > 0)
            {
                _cachedModels = parsed;
                int freeVisionCount = parsed.Count(m => m.IsFree && m.SupportsVision);
                int freeTextCount = parsed.Count(m => m.IsFree && !m.SupportsVision);
                return (true, parsed, $"Catálogo atualizado: {freeVisionCount} modelos gratuitos de Visão+Texto e {freeTextCount} modelos gratuitos de Texto disponíveis.");
            }

            var defaultList = GetFilteredDefaults(freeOnly);
            return (true, defaultList, "Lista padrão de modelos verificados carregada.");
        }
        catch (Exception ex)
        {
            Logger.Error("Falha ao consultar lista de modelos do OpenRouter.", ex);
            return (false, GetFilteredDefaults(freeOnly), "Sem conexão com o catálogo online do OpenRouter. Exibindo lista padrão verificada.");
        }
    }

    private static List<OpenRouterModelInfo> GetFilteredDefaults(bool freeOnly)
    {
        return freeOnly
            ? DefaultModels.Where(m => m.IsFree).ToList()
            : new List<OpenRouterModelInfo>(DefaultModels);
    }

    public static List<OpenRouterModelInfo> FilterAndParseModelsJson(string responseJson, bool freeOnly = true)
    {
        var result = new List<OpenRouterModelInfo>();
        try
        {
            var root = JsonNode.Parse(responseJson);
            var dataArray = root?["data"]?.AsArray();
            if (dataArray == null) return result;

            foreach (var item in dataArray)
            {
                if (item == null) continue;

                string id = item["id"]?.GetValue<string>()?.Trim() ?? string.Empty;
                string name = item["name"]?.GetValue<string>()?.Trim() ?? id;
                if (string.IsNullOrEmpty(id)) continue;

                // Excluir classificadores puros de moderação/segurança que não respondem prompts gerais
                if (id.Contains("content-safety", StringComparison.OrdinalIgnoreCase) ||
                    id.Contains("llama-guard", StringComparison.OrdinalIgnoreCase) ||
                    id.Contains("shieldgemma", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 1. Verificar modalidades de entrada e saída
                var arch = item["architecture"];
                var inputMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var outputMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var inArr = arch?["input_modalities"]?.AsArray();
                if (inArr != null)
                {
                    foreach (var m in inArr)
                    {
                        string? val = m?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(val)) inputMods.Add(val);
                    }
                }

                var outArr = arch?["output_modalities"]?.AsArray();
                if (outArr != null)
                {
                    foreach (var m in outArr)
                    {
                        string? val = m?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(val)) outputMods.Add(val);
                    }
                }

                // Fallback para o campo 'modality' (ex.: "text+image->text")
                string modalityStr = arch?["modality"]?.GetValue<string>() ?? string.Empty;
                if (inputMods.Count == 0 && modalityStr.Contains("->"))
                {
                    var parts = modalityStr.Split("->");
                    if (parts[0].Contains("text", StringComparison.OrdinalIgnoreCase)) inputMods.Add("text");
                    if (parts[0].Contains("image", StringComparison.OrdinalIgnoreCase)) inputMods.Add("image");
                    if (parts.Length > 1 && parts[1].Contains("text", StringComparison.OrdinalIgnoreCase)) outputMods.Add("text");
                    if (parts.Length > 1 && parts[1].Contains("image", StringComparison.OrdinalIgnoreCase)) outputMods.Add("image");
                }

                // Requisito obrigatório: saída deve ser texto, e NÃO deve ser geração de imagem ou áudio
                bool outputsText = outputMods.Contains("text");
                bool outputsImage = outputMods.Contains("image");
                bool outputsAudio = outputMods.Contains("audio");
                if (!outputsText || outputsImage || outputsAudio)
                {
                    continue;
                }

                bool supportsText = inputMods.Contains("text") || inputMods.Count > 0;
                bool supportsVision = inputMods.Contains("image");
                if (!supportsText && !supportsVision)
                {
                    continue;
                }

                // 2. Verificar preços (prompt, completion, image, request)
                var pricing = item["pricing"];
                bool isFree = IsZeroPrice(pricing?["prompt"]) &&
                              IsZeroPrice(pricing?["completion"]) &&
                              IsZeroPriceOrEmpty(pricing?["image"]) &&
                              IsZeroPriceOrEmpty(pricing?["request"]);

                if (freeOnly && !isFree)
                {
                    continue;
                }

                int contextLen = 0;
                try
                {
                    contextLen = item["context_length"]?.GetValue<int>() ?? 0;
                }
                catch { }

                string cleanName = Regex.Replace(name, @"\s*\(free\)\s*$", "", RegexOptions.IgnoreCase).Trim();
                string prefix = supportsVision ? "✦" : "📝";
                string priceTag = isFree ? "Gratuito" : "Pago";
                string modTag = supportsVision ? "Visão + Texto" : "Somente Texto";
                string displayName = $"{prefix} {cleanName} [{priceTag} • {modTag}]";

                string ctxLabel = contextLen >= 1000 ? $"{contextLen / 1000}K contexto" : "contexto padrão";
                string desc = supportsVision
                    ? $"Modelo multimodal ({modTag}, {priceTag}, {ctxLabel}) — compatível com análise de imagens e respostas em texto. ID: {id}"
                    : $"Modelo de linguagem ({modTag}, {priceTag}, {ctxLabel}) — indicado para tarefas de texto, perguntas e OCR. ID: {id}";

                result.Add(new OpenRouterModelInfo(
                    id,
                    cleanName,
                    displayName,
                    desc,
                    isFree,
                    supportsVision,
                    supportsText: true,
                    contextLength: contextLen));
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Erro ao processar JSON de modelos do OpenRouter.", ex);
        }

        // Ordenar priorizando:
        // 1º Gratuitos com Visão (Imagem + Texto -> Texto)
        // 2º Gratuitos Somente Texto
        // 3º Pagos com Visão
        // 4º Pagos Somente Texto
        return result
            .OrderByDescending(m => m.IsFree && m.SupportsVision)
            .ThenByDescending(m => m.IsFree)
            .ThenByDescending(m => m.SupportsVision)
            .ThenBy(m => GetPriorityOrder(m.Id))
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetPriorityOrder(string id)
    {
        if (id.Equals("google/gemma-4-31b-it:free", StringComparison.OrdinalIgnoreCase)) return 0;
        if (id.Equals("google/gemma-4-26b-a4b-it:free", StringComparison.OrdinalIgnoreCase)) return 1;
        if (id.Equals("nvidia/nemotron-3.5-super-vl:free", StringComparison.OrdinalIgnoreCase)) return 2;
        if (id.Equals("nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free", StringComparison.OrdinalIgnoreCase)) return 3;
        if (id.Equals("openrouter/free", StringComparison.OrdinalIgnoreCase)) return 4;
        return 10;
    }

    private static bool IsZeroPrice(JsonNode? node)
    {
        if (node == null) return false;
        string raw = node.ToString().Trim('"', ' ');
        if (string.IsNullOrEmpty(raw)) return false;
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val) && val == 0m;
    }

    private static bool IsZeroPriceOrEmpty(JsonNode? node)
    {
        if (node == null) return true;
        string raw = node.ToString().Trim('"', ' ');
        if (string.IsNullOrEmpty(raw)) return true;
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val) && val == 0m;
    }

    public async Task<(bool success, string message)> TestConnectionAsync(string apiKey, string modelName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "Chave da API do OpenRouter não configurada.");
        }

        string cleanKey = apiKey.Trim();
        string cleanModel = NormalizeModelName(modelName);

        try
        {
            var payload = new
            {
                model = cleanModel,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = "Responda apenas com a palavra: OK"
                    }
                },
                max_tokens = 16
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl);
            AddOpenRouterHeaders(req, cleanKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(req);
            string responseJson = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(responseJson);
                var errorNode = jsonNode?["error"];
                if (errorNode != null)
                {
                    return ParseHttpError(HttpStatusCode.BadRequest, responseJson, cleanModel, cleanKey);
                }

                string actualModel = jsonNode?["model"]?.GetValue<string>() ?? cleanModel;
                bool supportsVision = DoesModelSupportVision(cleanModel);
                string modInfo = supportsVision ? "Visão + Texto" : "Somente Texto";
                return (true, $"Conexão bem-sucedida com o OpenRouter! Modelo '{actualModel}' ({modInfo}) validado e operacional.");
            }

            // Se o erro não for 401 (ex.: 429 rate limit temporário do modelo gratuito), verifica se a chave em si é válida
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                var (_, errMsg) = ParseHttpError(response.StatusCode, responseJson, cleanModel, cleanKey);
                bool keyValid = await ValidateKeyOnlyAsync(cleanKey);
                if (keyValid && response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.NotFound)
                {
                    return (false, $"✓ Sua chave de API do OpenRouter é válida, porém o modelo '{cleanModel}' retornou:\n{errMsg}");
                }
                return (false, errMsg);
            }

            return ParseHttpError(response.StatusCode, responseJson, cleanModel, cleanKey);
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Falha de rede ao testar conexão com o OpenRouter.", ex);
            return (false, "Falha de conexão com a internet ou servidores do OpenRouter (openrouter.ai) inacessíveis.");
        }
        catch (TaskCanceledException)
        {
            return (false, "Tempo limite esgotado: O provedor no OpenRouter demorou mais de 50 segundos para responder.");
        }
        catch (Exception ex)
        {
            Logger.Error("Erro inesperado ao testar conexão com o OpenRouter.", ex);
            return (false, $"Erro inesperado: {SanitizeKeyFromText(ex.Message, cleanKey)}");
        }
    }

    private async Task<bool> ValidateKeyOnlyAsync(string apiKey)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, AuthKeyUrl);
            AddOpenRouterHeaders(req, apiKey);
            var resp = await HttpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static object BuildChatPayload(
        string model,
        IReadOnlyList<AiChatMessage> history,
        string newUserMessage,
        string? base64ImageJpeg = null,
        bool modelSupportsVision = true)
    {
        var messagesList = new List<object>();
        bool hasAttachedImage = false;
        bool shouldAttachImage = modelSupportsVision && !string.IsNullOrEmpty(base64ImageJpeg);

        foreach (var msg in history)
        {
            if (msg.IsError) continue;

            if (msg.IsUser)
            {
                if (!hasAttachedImage && shouldAttachImage)
                {
                    messagesList.Add(new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = msg.Text },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:image/jpeg;base64,{base64ImageJpeg}"
                                }
                            }
                        }
                    });
                    hasAttachedImage = true;
                }
                else
                {
                    messagesList.Add(new
                    {
                        role = "user",
                        content = msg.Text
                    });
                }
            }
            else if (msg.IsModel)
            {
                messagesList.Add(new
                {
                    role = "assistant",
                    content = msg.Text
                });
            }
        }

        // Adiciona a nova mensagem do usuário
        if (!hasAttachedImage && shouldAttachImage)
        {
            messagesList.Add(new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = newUserMessage },
                    new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = $"data:image/jpeg;base64,{base64ImageJpeg}"
                        }
                    }
                }
            });
        }
        else
        {
            messagesList.Add(new
            {
                role = "user",
                content = newUserMessage
            });
        }

        return new
        {
            model = NormalizeModelName(model),
            messages = messagesList
        };
    }

    public async Task<(bool success, string responseText, string errorMessage)> SendChatMessageAsync(
        IReadOnlyList<AiChatMessage> history,
        string newUserMessage,
        Bitmap? imageContext,
        CancellationToken ct = default)
    {
        string apiKey = SettingsService.Instance.GetOpenRouterApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, string.Empty, "Chave da API do OpenRouter não configurada. Abra as Configurações (aba Inteligência Artificial) e informe sua chave do OpenRouter.");
        }

        string model = NormalizeModelName(SettingsService.Instance.Current.OpenRouterModel);
        bool supportsVision = DoesModelSupportVision(model);

        try
        {
            string? base64Image = null;
            string effectiveUserMessage = newUserMessage;

            if (imageContext != null)
            {
                if (supportsVision)
                {
                    byte[] jpgBytes = ScreenCaptureService.ToJpgByteArray(imageContext, 90);
                    base64Image = Convert.ToBase64String(jpgBytes);
                }
                else
                {
                    // Modelo somente de texto: se o histórico ainda não contém o texto OCR, extrai via OCR local automaticamente
                    bool historyAlreadyHasOcr = history.Any(m => m.Text.Contains("Texto extraído via OCR", StringComparison.OrdinalIgnoreCase));
                    if (!historyAlreadyHasOcr)
                    {
                        var ocrResult = await OcrService.RecognizeTextAsync(imageContext);
                        if (ocrResult.Success && !string.IsNullOrWhiteSpace(ocrResult.Text))
                        {
                            effectiveUserMessage = $"{newUserMessage}\n\n[Contexto textual extraído da captura de tela via OCR local]:\n{ocrResult.Text}";
                        }
                        else
                        {
                            effectiveUserMessage = $"{newUserMessage}\n\n[Nota: O modelo selecionado ('{model}') é somente de texto e nenhum texto legível foi detectado via OCR local na captura. Para análise visual de imagens sem texto, selecione um modelo com 'Visão + Texto' nas configurações do OpenRouter.]";
                        }
                    }
                }
            }

            var (success, text, errMsg, shouldRetryTextOnly) = await SendCompletionRequestAsync(
                apiKey, model, history, effectiveUserMessage, base64Image, supportsVision, ct);

            // Se um modelo customizado rejeitar entrada de imagem com 400, faz fallback automático para modo texto + OCR
            if (!success && shouldRetryTextOnly && imageContext != null)
            {
                Logger.Info($"Modelo '{model}' não suporta imagem direta; realizando fallback automático com OCR local.");
                var ocrFallback = await OcrService.RecognizeTextAsync(imageContext);
                string fallbackPrompt = ocrFallback.Success && !string.IsNullOrWhiteSpace(ocrFallback.Text)
                    ? $"{newUserMessage}\n\n[Texto extraído da imagem via OCR local]:\n{ocrFallback.Text}"
                    : newUserMessage;

                var retry = await SendCompletionRequestAsync(apiKey, model, history, fallbackPrompt, null, false, ct);
                return (retry.success, retry.responseText, retry.errorMessage);
            }

            return (success, text, errMsg);
        }
        catch (OperationCanceledException)
        {
            return (false, string.Empty, "Operação cancelada pelo usuário.");
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Falha de rede no chat com o OpenRouter.", ex);
            return (false, string.Empty, "Falha de conexão com a internet ou servidores do OpenRouter indisponíveis.");
        }
        catch (Exception ex)
        {
            Logger.LogException("OpenRouterService", "SendChatMessageAsync", ex);
            return (false, string.Empty, $"Erro durante a comunicação com o OpenRouter: {SanitizeKeyFromText(ex.Message, apiKey)}");
        }
    }

    public async Task<AiResult> ExecuteActionAsync(
        AiActionType action,
        Bitmap bitmap,
        string? customQuestion = null,
        CancellationToken ct = default)
    {
        // A função de pesquisa na web é mantida conforme sua implementação original no Gemini
        if (action == AiActionType.SearchWeb)
        {
            return await GeminiService.Instance.ExecuteActionAsync(action, bitmap, customQuestion, ct);
        }

        string title = GeminiService.GetActionTitle(action);
        string prompt = GeminiService.BuildPromptForActionStatic(action, customQuestion);

        var result = new AiResult
        {
            ActionType = action,
            ActionTitle = title,
            Prompt = prompt,
            Timestamp = DateTime.Now
        };

        var (success, responseText, errorMsg) = await SendChatMessageAsync(
            Array.Empty<AiChatMessage>(),
            prompt,
            bitmap,
            ct);

        result.IsSuccess = success;
        result.ResponseText = responseText;
        result.ErrorMessage = errorMsg;
        return result;
    }

    private async Task<(bool success, string responseText, string errorMessage, bool shouldRetryTextOnly)> SendCompletionRequestAsync(
        string apiKey,
        string model,
        IReadOnlyList<AiChatMessage> history,
        string userMessage,
        string? base64Image,
        bool supportsVision,
        CancellationToken ct)
    {
        var payload = BuildChatPayload(model, history, userMessage, base64Image, supportsVision);
        string jsonBody = JsonSerializer.Serialize(payload);

        using var req = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl);
        AddOpenRouterHeaders(req, apiKey);
        req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var response = await HttpClient.SendAsync(req, ct);
        string responseJson = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            bool imageUnsupportedError = response.StatusCode == HttpStatusCode.BadRequest &&
                                         supportsVision &&
                                         !string.IsNullOrEmpty(base64Image) &&
                                         (responseJson.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                                          responseJson.Contains("vision", StringComparison.OrdinalIgnoreCase) ||
                                          responseJson.Contains("multimodal", StringComparison.OrdinalIgnoreCase));

            var (_, errorMsg) = ParseHttpError(response.StatusCode, responseJson, model, apiKey);
            return (false, string.Empty, errorMsg, imageUnsupportedError);
        }

        var jsonNode = JsonNode.Parse(responseJson);
        if (jsonNode?["error"] != null)
        {
            var (_, errorMsg) = ParseHttpError(HttpStatusCode.BadRequest, responseJson, model, apiKey);
            return (false, string.Empty, errorMsg, false);
        }

        var choices = jsonNode?["choices"]?.AsArray();
        if (choices == null || choices.Count == 0)
        {
            return (false, string.Empty, "O modelo no OpenRouter não retornou respostas para esta solicitação.", false);
        }

        var messageNode = choices[0]?["message"];
        string? content = ExtractMessageText(messageNode?["content"]);

        if (string.IsNullOrWhiteSpace(content))
        {
            string finishReason = choices[0]?["finish_reason"]?.GetValue<string>() ?? string.Empty;
            if (!string.IsNullOrEmpty(finishReason))
            {
                return (false, string.Empty, $"O modelo finalizou sem retornar texto (Motivo: {finishReason}).", false);
            }
            content = "(Resposta sem texto retornado pelo modelo)";
        }

        return (true, content.Trim(), string.Empty, false);
    }

    private static string? ExtractMessageText(JsonNode? contentNode)
    {
        if (contentNode == null) return null;
        if (contentNode is JsonValue val && val.TryGetValue<string>(out string? str))
        {
            return str;
        }
        if (contentNode is JsonArray arr)
        {
            var sb = new StringBuilder();
            foreach (var part in arr)
            {
                string? text = part?["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    sb.Append(text);
                }
            }
            return sb.ToString();
        }
        return contentNode.ToString();
    }

    public static (bool success, string message) ParseHttpError(
        HttpStatusCode statusCode,
        string responseJson,
        string? modelName = null,
        string? apiKey = null)
    {
        string detail = string.Empty;
        string providerDetail = string.Empty;

        try
        {
            var node = JsonNode.Parse(responseJson);
            var errNode = node?["error"];
            if (errNode != null)
            {
                detail = errNode["message"]?.GetValue<string>() ?? string.Empty;
                string? rawMeta = errNode["metadata"]?["raw"]?.GetValue<string>();
                string? providerName = errNode["metadata"]?["provider_name"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(providerName))
                {
                    providerDetail = $"Provedor upstream: {providerName}.";
                }
                if (!string.IsNullOrWhiteSpace(rawMeta) && !detail.Contains(rawMeta, StringComparison.OrdinalIgnoreCase))
                {
                    providerDetail = string.IsNullOrEmpty(providerDetail) ? rawMeta : $"{providerDetail} {rawMeta}";
                }
            }
        }
        catch { }

        detail = SanitizeKeyFromText(detail, apiKey);
        providerDetail = SanitizeKeyFromText(providerDetail, apiKey);

        string combinedDetail = string.IsNullOrWhiteSpace(providerDetail)
            ? detail
            : $"{detail} ({providerDetail})";

        string message = statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                $"Chave da API do OpenRouter inválida, ausente ou expirada (401):\nVerifique se sua chave (iniciada por 'sk-or-v1-...') foi copiada corretamente do painel oficial em openrouter.ai/keys.{(string.IsNullOrWhiteSpace(combinedDetail) ? string.Empty : $"\nDetalhes: {combinedDetail}")}",

            HttpStatusCode.PaymentRequired =>
                $"Créditos insuficientes no OpenRouter (402):\nSua conta não possui saldo de créditos para executar o modelo '{modelName ?? "selecionado"}'.\n\nSolução: Selecione um modelo marcado como [Gratuito] (final ':free' ou 'openrouter/free') nas Configurações de IA, ou adicione créditos em openrouter.ai/credits.",

            HttpStatusCode.Forbidden =>
                combinedDetail.Contains("data policy", StringComparison.OrdinalIgnoreCase) || combinedDetail.Contains("privacy", StringComparison.OrdinalIgnoreCase)
                    ? $"Configuração de privacidade do OpenRouter necessária para modelos gratuitos (403):\nO OpenRouter exige que você habilite o uso de modelos gratuitos nas configurações de privacidade da sua conta.\n\nAcesse: https://openrouter.ai/settings/privacy e ative as opções para modelos gratuitos (:free).\nDetalhes: {combinedDetail}"
                    : $"Acesso negado ou requisição bloqueada no OpenRouter (403):\n{(string.IsNullOrWhiteSpace(combinedDetail) ? "Sua chave não tem permissão para este modelo ou o conteúdo foi filtrado." : combinedDetail)}",

            HttpStatusCode.NotFound =>
                $"Modelo indisponível ou não encontrado no OpenRouter (404):\nO modelo '{modelName ?? "selecionado"}' não possui provedores ativos no momento ou foi removido do catálogo.\n\nSolução: Abra as Configurações de IA, clique em 'Atualizar Modelos' e selecione outro modelo gratuito compatível (ou use 'openrouter/free').\nDetalhes: {(string.IsNullOrWhiteSpace(combinedDetail) ? "No endpoints found" : combinedDetail)}",

            HttpStatusCode.TooManyRequests =>
                $"Limite de requisições atingido no OpenRouter (429 - Rate Limit):\n{(string.IsNullOrWhiteSpace(combinedDetail) ? "O limite de chamadas gratuitas por minuto ou por dia foi atingido." : combinedDetail)}\n\nDica: Aguarde alguns segundos antes de tentar novamente ou alterne para outro modelo gratuito nas Configurações (como 'openrouter/free' ou 'google/gemma-4-31b-it:free').",

            HttpStatusCode.BadRequest =>
                combinedDetail.Contains("image", StringComparison.OrdinalIgnoreCase) || combinedDetail.Contains("vision", StringComparison.OrdinalIgnoreCase)
                    ? $"O modelo selecionado ('{modelName}') não suporta entrada direta de imagens (400):\nPara analisar capturas visualmente, selecione um modelo marcado com [Visão + Texto] nas configurações.\nDetalhes: {combinedDetail}"
                    : $"Requisição inválida no OpenRouter (400):\n{(string.IsNullOrWhiteSpace(combinedDetail) ? "Verifique se o modelo selecionado é compatível com a operação." : combinedDetail)}",

            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
                $"Tempo limite esgotado no provedor do OpenRouter ({(int)statusCode}):\nO modelo '{modelName}' demorou muito para responder. Tente novamente ou selecione outro modelo.",

            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable =>
                $"Indisponibilidade temporária no provedor do OpenRouter ({(int)statusCode}):\n{(string.IsNullOrWhiteSpace(combinedDetail) ? "O provedor upstream do modelo está temporariamente sobrecarregado." : combinedDetail)}\n\nTente novamente em alguns instantes ou troque de modelo nas configurações.",

            _ =>
                $"Erro retornado pela API do OpenRouter ({(int)statusCode}):\n{(string.IsNullOrWhiteSpace(combinedDetail) ? "Falha desconhecida na comunicação." : combinedDetail)}"
        };

        return (false, message);
    }

    private static string SanitizeKeyFromText(string? input, string? apiKey)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        string sanitized = input;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            sanitized = sanitized.Replace(apiKey.Trim(), "[CHAVE_PROTEGIDA]", StringComparison.OrdinalIgnoreCase);
        }
        sanitized = Regex.Replace(sanitized, @"sk-or-[a-zA-Z0-9\-_]{8,}", "[CHAVE_PROTEGIDA]", RegexOptions.IgnoreCase);
        return sanitized;
    }
}

public class OpenRouterModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsFree { get; set; } = true;
    public bool SupportsVision { get; set; } = true;
    public bool SupportsText { get; set; } = true;
    public int ContextLength { get; set; } = 0;

    public OpenRouterModelInfo() { }

    public OpenRouterModelInfo(
        string id,
        string name,
        string displayName,
        string description,
        bool isFree,
        bool supportsVision,
        bool supportsText,
        int contextLength = 0)
    {
        Id = id;
        Name = name;
        DisplayName = displayName;
        Description = description;
        IsFree = isFree;
        SupportsVision = supportsVision;
        SupportsText = supportsText;
        ContextLength = contextLength;
    }

    public override string ToString() => DisplayName;
}
