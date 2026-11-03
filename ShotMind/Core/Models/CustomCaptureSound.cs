namespace ShotMind.Core.Models;

/// <summary>
/// Som de captura importado pelo usuário. O arquivo é copiado para
/// %AppData%\ShotMind\sounds\custom, portanto não depende do caminho original.
/// </summary>
public class CustomCaptureSound
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
