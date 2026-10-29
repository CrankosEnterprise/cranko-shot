using System;

namespace ShotMind.Core.Models;

public class AiChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Role { get; set; } = "user"; // "user" | "model" | "system_error"
    public string Text { get; set; } = string.Empty;
    public string ProviderLabel { get; set; } = "✦ Gemini";
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public bool IsUser => Role == "user";
    public bool IsModel => Role == "model";
    public bool IsError => Role == "system_error";

    public string TimeFormatted => Timestamp.ToString("HH:mm");
}
