namespace ShotMind.Core;

public enum ToolType
{
    Select,
    Arrow,
    Line,
    Rectangle,
    Ellipse,
    Pen,
    Highlighter,
    Text,
    Blur,
    StepMarker
}

public enum StepMarkerStyle
{
    Numbers,     // 1, 2, 3...
    Uppercase,   // A, B, C...
    Lowercase,   // a, b, c...
    Dots         // Círculos sólidos sem texto
}

public enum DefaultCaptureAction
{
    CopyToClipboard,
    SaveToFile,
    CopyAndSave,
    AskUser
}

public enum ImageFormatType
{
    Png,
    Jpg
}

public enum AiActionType
{
    Analyze,
    ExtractText,
    Translate,
    Summarize,
    ExplainError,
    AskQuestion,
    IdentifyElements,
    SearchWeb
}

public enum AiStatus
{
    Idle,
    Loading,
    Success,
    Error
}

public enum ObfuscationStyle
{
    Pixelate,
    Blur
}

public enum CrosshairStyle
{
    Simple = 0,
    Open = 1,
    Closed = 2,
    CenterDot = 3,
    LinesAndDot = 4
}

public enum AiProviderType
{
    Gemini = 0,
    OpenRouter = 1
}
