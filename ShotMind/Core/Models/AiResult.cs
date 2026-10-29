using System;
using System.Collections.Generic;

namespace ShotMind.Core.Models;

public class WebSource
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class AiResult
{
    public AiActionType ActionType { get; set; }
    public string ActionTitle { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string ResponseText { get; set; } = string.Empty;
    public List<WebSource> Sources { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
