using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ShotMind.Core.Models;

namespace ShotMind.Core.Services;

public record ThemeInfo(
    string Name,
    string DisplayName,
    Color Primary,
    Color Hover,
    Color Dark,
    Color Subtle,
    Color Light,
    Color Border,
    Color UserBubble,
    Color Foreground
)
{
    public string PrimaryHex => $"#{Primary.R:X2}{Primary.G:X2}{Primary.B:X2}";
    public SolidColorBrush PrimaryBrush
    {
        get
        {
            var brush = new SolidColorBrush(Primary);
            brush.Freeze();
            return brush;
        }
    }
}

public class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    public static readonly ThemeInfo PurpleTheme = new(
        Name: "Purple",
        DisplayName: "Roxo",
        Primary: Color.FromRgb(0x8B, 0x5C, 0xF6),      // #8B5CF6
        Hover: Color.FromRgb(0x7C, 0x3A, 0xED),        // #7C3AED
        Dark: Color.FromRgb(0x6D, 0x28, 0xD9),         // #6D28D9
        Subtle: Color.FromRgb(0x22, 0x1C, 0x35),       // #221C35
        Light: Color.FromRgb(0xC4, 0xB5, 0xFD),        // #C4B5FD
        Border: Color.FromRgb(0xA7, 0x8B, 0xFA),       // #A78BFA
        UserBubble: Color.FromRgb(0x5B, 0x21, 0xB6),   // #5B21B6
        Foreground: Color.FromRgb(0xFF, 0xFF, 0xFF)    // #FFFFFF
    );

    public static readonly ThemeInfo RedTheme = new(
        Name: "Red",
        DisplayName: "Vermelho",
        Primary: Color.FromRgb(0xEF, 0x44, 0x44),      // #EF4444
        Hover: Color.FromRgb(0xDC, 0x26, 0x26),        // #DC2626
        Dark: Color.FromRgb(0xB9, 0x1C, 0x1C),         // #B91C1C
        Subtle: Color.FromRgb(0x32, 0x18, 0x1A),       // #32181A
        Light: Color.FromRgb(0xFC, 0xA5, 0xA5),        // #FCA5A5
        Border: Color.FromRgb(0xF8, 0x71, 0x71),       // #F87171
        UserBubble: Color.FromRgb(0x99, 0x1B, 0x1B),   // #991B1B
        Foreground: Color.FromRgb(0xFF, 0xFF, 0xFF)    // #FFFFFF
    );

    public static readonly ThemeInfo PinkTheme = new(
        Name: "Pink",
        DisplayName: "Rosa",
        Primary: Color.FromRgb(0xEC, 0x48, 0x99),      // #EC4899
        Hover: Color.FromRgb(0xDB, 0x27, 0x77),        // #DB2777
        Dark: Color.FromRgb(0xBE, 0x18, 0x5D),         // #BE185D
        Subtle: Color.FromRgb(0x32, 0x16, 0x26),       // #321626
        Light: Color.FromRgb(0xF9, 0xA8, 0xD4),        // #F9A8D4
        Border: Color.FromRgb(0xF4, 0x72, 0xB6),       // #F472B6
        UserBubble: Color.FromRgb(0x9D, 0x17, 0x4D),   // #9D174D
        Foreground: Color.FromRgb(0xFF, 0xFF, 0xFF)    // #FFFFFF
    );

    public static readonly ThemeInfo GreenTheme = new(
        Name: "Green",
        DisplayName: "Verde",
        Primary: Color.FromRgb(0x10, 0xB9, 0x81),      // #10B981
        Hover: Color.FromRgb(0x05, 0x96, 0x69),        // #059669
        Dark: Color.FromRgb(0x04, 0x78, 0x57),         // #047857
        Subtle: Color.FromRgb(0x13, 0x2B, 0x21),       // #132B21
        Light: Color.FromRgb(0x6E, 0xE7, 0xB7),        // #6EE7B7
        Border: Color.FromRgb(0x34, 0xD3, 0x99),       // #34D399
        UserBubble: Color.FromRgb(0x06, 0x5F, 0x46),   // #065F46
        Foreground: Color.FromRgb(0xFF, 0xFF, 0xFF)    // #FFFFFF
    );

    public static readonly ThemeInfo YellowTheme = new(
        Name: "Yellow",
        DisplayName: "Amarelo",
        Primary: Color.FromRgb(0xF5, 0x9E, 0x0B),      // #F59E0B
        Hover: Color.FromRgb(0xD9, 0x77, 0x06),        // #D97706
        Dark: Color.FromRgb(0xB4, 0x53, 0x09),         // #B45309
        Subtle: Color.FromRgb(0x31, 0x23, 0x11),       // #312311
        Light: Color.FromRgb(0xFD, 0xE6, 0x8A),        // #FDE68A
        Border: Color.FromRgb(0xFB, 0xBF, 0x24),       // #FBBF24
        UserBubble: Color.FromRgb(0x92, 0x40, 0x0E),   // #92400E
        Foreground: Color.FromRgb(0x18, 0x18, 0x1B)    // #18181B (contraste máximo sobre fundo amarelo âmbar)
    );

    public static readonly ThemeInfo GrayTheme = new(
        Name: "Gray",
        DisplayName: "Cinza",
        Primary: Color.FromRgb(0x71, 0x71, 0x7A),      // #71717A (Cinza Zinc 500 moderno)
        Hover: Color.FromRgb(0x52, 0x52, 0x5B),        // #52525B (Zinc 600)
        Dark: Color.FromRgb(0x3F, 0x3F, 0x46),         // #3F3F46 (Zinc 700)
        Subtle: Color.FromRgb(0x27, 0x27, 0x2A),       // #27272A (Zinc 800)
        Light: Color.FromRgb(0xA1, 0xA1, 0xAA),        // #A1A1AA (Zinc 400)
        Border: Color.FromRgb(0xA1, 0xA1, 0xAA),       // #A1A1AA (Zinc 400)
        UserBubble: Color.FromRgb(0x3F, 0x3F, 0x46),   // #3F3F46 (Zinc 700)
        Foreground: Color.FromRgb(0xFF, 0xFF, 0xFF)    // #FFFFFF (Contraste máximo)
    );

    [Obsolete("O tema Branco foi substituído pelo tema Cinza.")]
    public static ThemeInfo WhiteTheme => GrayTheme;

    public static readonly ThemeInfo OrangeTheme = new(
        Name: "Orange",
        DisplayName: "Laranja",
        Primary: Color.FromRgb(0xF9, 0x73, 0x16),      // #F97316
        Hover: Color.FromRgb(0xEA, 0x58, 0x0C),        // #EA580C
        Dark: Color.FromRgb(0xC2, 0x41, 0x0C),         // #C2410C
        Subtle: Color.FromRgb(0x33, 0x1D, 0x14),       // #331D14
        Light: Color.FromRgb(0xFE, 0xD7, 0xAA),        // #FED7AA
        Border: Color.FromRgb(0xFB, 0x92, 0x3C),       // #FB923C
        UserBubble: Color.FromRgb(0x9A, 0x34, 0x12),   // #9A3412
        Foreground: Color.FromRgb(0xFF, 0xFF, 0xFF)    // #FFFFFF
    );

    public static readonly ThemeInfo BlueTheme = new(
        Name: "Blue",
        DisplayName: "Azul",
        Primary: Color.FromRgb(0x3B, 0x82, 0xF6),      // #3B82F6
        Hover: Color.FromRgb(0x25, 0x63, 0xEB),        // #2563EB
        Dark: Color.FromRgb(0x1D, 0x4E, 0xD8),         // #1D4ED8
        Subtle: Color.FromRgb(0x14, 0x22, 0x38),       // #142238
        Light: Color.FromRgb(0xBF, 0xDB, 0xFE),        // #BFDBFE
        Border: Color.FromRgb(0x60, 0xA5, 0xFA),       // #60A5FA
        UserBubble: Color.FromRgb(0x1E, 0x40, 0xAF),   // #1E40AF
        Foreground: Color.FromRgb(0xFF, 0xFF, 0xFF)    // #FFFFFF
    );

    public static readonly IReadOnlyList<ThemeInfo> AllThemes = new[]
    {
        PurpleTheme,
        RedTheme,
        PinkTheme,
        GreenTheme,
        YellowTheme,
        GrayTheme,
        OrangeTheme,
        BlueTheme
    };

    public string CurrentThemeName { get; private set; } = PurpleTheme.Name;
    public ThemeInfo CurrentTheme { get; private set; } = PurpleTheme;

    public event Action<ThemeInfo>? ThemeChanged;

    private ThemeService() { }

    public void Initialize()
    {
        string saved = SettingsService.Instance.Current.AccentTheme;
        ApplyTheme(saved, persist: false);
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public bool ApplyTheme(string? themeName, bool persist = true)
    {
        // Migração segura: se a preferência gravada for White, migra para Gray
        if (string.Equals(themeName, "White", StringComparison.OrdinalIgnoreCase))
        {
            themeName = "Gray";
        }

        var target = AllThemes.FirstOrDefault(t => t.Name.Equals(themeName, StringComparison.OrdinalIgnoreCase))
                     ?? PurpleTheme;

        CurrentThemeName = target.Name;
        CurrentTheme = target;

        if (persist)
        {
            SettingsService.Instance.Current.AccentTheme = target.Name;
            SettingsService.Instance.Save();
        }

        void UpdateResources()
        {
            var res = Application.Current?.Resources;
            if (res == null) return;

            // Accent Palette Keys
            res["AccentPrimaryColor"] = target.Primary;
            res["AccentHoverColor"] = target.Hover;
            res["AccentDarkColor"] = target.Dark;

            res["AccentPrimaryBrush"] = CreateFrozenBrush(target.Primary);
            res["AccentHoverBrush"] = CreateFrozenBrush(target.Hover);
            res["AccentDarkBrush"] = CreateFrozenBrush(target.Dark);
            res["AccentSubtleBrush"] = CreateFrozenBrush(target.Subtle);
            res["AccentLightBrush"] = CreateFrozenBrush(target.Light);
            res["AccentBorderBrush"] = CreateFrozenBrush(target.Border);
            res["AccentUserBubbleBrush"] = CreateFrozenBrush(target.UserBubble);
            res["AccentForegroundBrush"] = CreateFrozenBrush(target.Foreground);

            // Backward compatibility with Purple* resources
            res["PurplePrimaryColor"] = target.Primary;
            res["PurpleHoverColor"] = target.Hover;
            res["PurpleDarkColor"] = target.Dark;

            res["PurplePrimaryBrush"] = res["AccentPrimaryBrush"];
            res["PurpleHoverBrush"] = res["AccentHoverBrush"];
            res["PurpleDarkBrush"] = res["AccentDarkBrush"];

            ThemeChanged?.Invoke(target);
            Logger.Info($"Tema de cor aplicado globalmente: {target.DisplayName} ({target.Name})");
        }

        if (Application.Current != null)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                UpdateResources();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(UpdateResources);
            }
        }

        return true;
    }
}
