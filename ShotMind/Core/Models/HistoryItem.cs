using System;

namespace ShotMind.Core.Models;

public class HistoryItem
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "PNG";
    public string ThumbnailBase64 { get; set; } = string.Empty;
    public string? OcrText { get; set; }
    public string? AiSummary { get; set; }

    // Helper property to check if file still exists on disk
    public bool FileExists => !string.IsNullOrEmpty(FilePath) && System.IO.File.Exists(FilePath);
}
