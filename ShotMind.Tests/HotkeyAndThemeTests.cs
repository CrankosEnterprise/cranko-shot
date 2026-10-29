using System;
using System.Linq;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShotMind.Core.Services;

namespace ShotMind.Tests;

[TestClass]
public class HotkeyAndThemeTests
{
    [TestMethod]
    public void HotkeyService_FormatKeyName_SnapshotReturnsPrintScreen()
    {
        Assert.AreEqual("Print Screen", HotkeyService.FormatKeyName("Snapshot"));
        Assert.AreEqual("Print Screen", HotkeyService.FormatKeyName("PrintScreen"));
        Assert.AreEqual("Print Screen", HotkeyService.FormatKeyName("PrtScn"));
        Assert.AreEqual("Print Screen", HotkeyService.FormatKeyName(null));
        Assert.AreEqual("F11", HotkeyService.FormatKeyName("F11"));
        Assert.AreEqual("P", HotkeyService.FormatKeyName("p"));
    }

    [TestMethod]
    public void HotkeyService_FormatHotkeyDisplay_FormatsCorrectly()
    {
        Assert.AreEqual("Print Screen", HotkeyService.FormatHotkeyDisplay("None", "Snapshot"));
        Assert.AreEqual("Ctrl + Shift + S", HotkeyService.FormatHotkeyDisplay("Ctrl + Shift", "S"));
        Assert.AreEqual("Alt + F11", HotkeyService.FormatHotkeyDisplay("Alt", "F11"));
        Assert.AreEqual("Ctrl + Alt + Shift + P", HotkeyService.FormatHotkeyDisplay("Ctrl + Alt + Shift", "P"));
        Assert.AreEqual("Win + Shift + C", HotkeyService.FormatHotkeyDisplay("Win + Shift", "C"));
    }

    [TestMethod]
    public void HotkeyService_GetModifierFlags_ParsesModifiersAccurately()
    {
        uint none = HotkeyService.GetModifierFlags("None");
        Assert.AreEqual(HotkeyService.MOD_NOREPEAT, none);

        uint ctrl = HotkeyService.GetModifierFlags("Ctrl");
        Assert.IsTrue((ctrl & HotkeyService.MOD_CONTROL) != 0);

        uint altShift = HotkeyService.GetModifierFlags("Alt + Shift");
        Assert.IsTrue((altShift & HotkeyService.MOD_ALT) != 0);
        Assert.IsTrue((altShift & HotkeyService.MOD_SHIFT) != 0);

        uint winShift = HotkeyService.GetModifierFlags("Win + Shift");
        Assert.IsTrue((winShift & HotkeyService.MOD_WIN) != 0);
        Assert.IsTrue((winShift & HotkeyService.MOD_SHIFT) != 0);
    }

    [TestMethod]
    public void HotkeyService_GetVirtualKey_ReturnsCorrectVK()
    {
        Assert.AreEqual(0x2Cu, HotkeyService.GetVirtualKey("Snapshot"));
        Assert.AreEqual(0x2Cu, HotkeyService.GetVirtualKey("Print Screen"));
        Assert.AreEqual(0x2Cu, HotkeyService.GetVirtualKey(""));

        // Letters and function keys
        uint vkF11 = HotkeyService.GetVirtualKey("F11");
        Assert.AreNotEqual(0u, vkF11);
        Assert.AreNotEqual(0x2Cu, vkF11);
    }

    [TestMethod]
    public void HotkeyService_IsCombinationAllowed_ValidatesSafely()
    {
        // Print Screen alone is allowed
        Assert.IsTrue(HotkeyService.IsCombinationAllowed("None", "Snapshot", out string reason));
        Assert.IsTrue(string.IsNullOrEmpty(reason));

        // Function keys alone are allowed
        Assert.IsTrue(HotkeyService.IsCombinationAllowed("None", "F11", out _));
        Assert.IsTrue(HotkeyService.IsCombinationAllowed(null, "F12", out _));

        // Letters without modifiers are BLOCKED to avoid hijacking normal typing
        Assert.IsFalse(HotkeyService.IsCombinationAllowed("None", "A", out string letterReason));
        Assert.IsTrue(letterReason.Contains("digitação", StringComparison.OrdinalIgnoreCase));

        // Letters WITH modifiers are ALLOWED
        Assert.IsTrue(HotkeyService.IsCombinationAllowed("Ctrl + Shift", "S", out _));
        Assert.IsTrue(HotkeyService.IsCombinationAllowed("Alt", "P", out _));

        // System reserved combinations are blocked
        Assert.IsFalse(HotkeyService.IsCombinationAllowed("Win", "L", out string winLReason));
        Assert.IsTrue(winLReason.Contains("Win + L", StringComparison.OrdinalIgnoreCase));

        Assert.IsFalse(HotkeyService.IsCombinationAllowed("Ctrl + Alt", "Delete", out string cadReason));
        Assert.IsTrue(cadReason.Contains("Ctrl + Alt + Del", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ThemeService_AllEightThemesExistWithExpectedColors()
    {
        Assert.AreEqual(8, ThemeService.AllThemes.Count);

        var names = ThemeService.AllThemes.Select(t => t.Name).ToList();
        CollectionAssert.Contains(names, "Purple");
        CollectionAssert.Contains(names, "Red");
        CollectionAssert.Contains(names, "Pink");
        CollectionAssert.Contains(names, "Green");
        CollectionAssert.Contains(names, "Yellow");
        CollectionAssert.Contains(names, "Gray");
        CollectionAssert.Contains(names, "Orange");
        CollectionAssert.Contains(names, "Blue");

        var displayNames = ThemeService.AllThemes.Select(t => t.DisplayName).ToList();
        CollectionAssert.Contains(displayNames, "Roxo");
        CollectionAssert.Contains(displayNames, "Vermelho");
        CollectionAssert.Contains(displayNames, "Rosa");
        CollectionAssert.Contains(displayNames, "Verde");
        CollectionAssert.Contains(displayNames, "Amarelo");
        CollectionAssert.Contains(displayNames, "Cinza");
        CollectionAssert.Contains(displayNames, "Laranja");
        CollectionAssert.Contains(displayNames, "Azul");
    }

    [TestMethod]
    public void ThemeService_ContrastThemes_HaveProperForeground()
    {
        // Yellow must have dark foreground for readability
        var yellow = ThemeService.YellowTheme;
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B), yellow.Primary);
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0x18, 0x18, 0x1B), yellow.Foreground);

        // Gray theme (replaces White) uses elegant zinc #71717A with white foreground
        var gray = ThemeService.GrayTheme;
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0x71, 0x71, 0x7A), gray.Primary);
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF), gray.Foreground);

        // Standard vibrant themes have white foreground
        var purple = ThemeService.PurpleTheme;
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF), purple.Foreground);

        var orange = ThemeService.OrangeTheme;
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0xF9, 0x73, 0x16), orange.Primary);
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF), orange.Foreground);

        var blue = ThemeService.BlueTheme;
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0x3B, 0x82, 0xF6), blue.Primary);
        Assert.AreEqual(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF), blue.Foreground);
    }

    [TestMethod]
    public void ThemeService_ApplyTheme_FallsBackToPurpleWhenUnknown()
    {
        ThemeService.Instance.ApplyTheme("InvalidUnknownTheme", persist: false);
        Assert.AreEqual("Purple", ThemeService.Instance.CurrentThemeName);
        Assert.AreEqual("Roxo", ThemeService.Instance.CurrentTheme.DisplayName);

        ThemeService.Instance.ApplyTheme("Blue", persist: false);
        Assert.AreEqual("Blue", ThemeService.Instance.CurrentThemeName);
        Assert.AreEqual("Azul", ThemeService.Instance.CurrentTheme.DisplayName);

        // White safely migrates to Gray
        ThemeService.Instance.ApplyTheme("White", persist: false);
        Assert.AreEqual("Gray", ThemeService.Instance.CurrentThemeName);
        Assert.AreEqual("Cinza", ThemeService.Instance.CurrentTheme.DisplayName);

        // Direct Gray theme
        ThemeService.Instance.ApplyTheme("Gray", persist: false);
        Assert.AreEqual("Gray", ThemeService.Instance.CurrentThemeName);
        Assert.AreEqual("Cinza", ThemeService.Instance.CurrentTheme.DisplayName);

        // Restore default
        ThemeService.Instance.ApplyTheme("Purple", persist: false);
    }

    [TestMethod]
    public void SettingsService_ValidationAndAtomicPersistence_GuaranteesValidSettings()
    {
        var settings = SettingsService.Instance.Current;
        Assert.IsNotNull(settings);

        // Ensure quality and intensities are safely clamped
        Assert.IsTrue(settings.JpgQuality >= 10 && settings.JpgQuality <= 100);
        Assert.IsTrue(settings.ObfuscationIntensity >= 4 && settings.ObfuscationIntensity <= 64);
        Assert.IsFalse(string.IsNullOrWhiteSpace(settings.FilenamePattern));

        // Test saving maintains persistence integrity
        SettingsService.Instance.Save();
        Assert.IsNotNull(SettingsService.Instance.Current);
    }

    [TestMethod]
    public void ShortcutManager_NormalizeShortcutString_OrdersModifiersConsistently()
    {
        Assert.AreEqual("Ctrl + Shift + P", ShortcutManager.NormalizeShortcutString("Shift + Ctrl + P"));
        Assert.AreEqual("Ctrl + C", ShortcutManager.NormalizeShortcutString("ctrl+c"));
        Assert.AreEqual("Ctrl + Alt + S", ShortcutManager.NormalizeShortcutString("Alt + Ctrl + S"));
        Assert.AreEqual("Esc", ShortcutManager.NormalizeShortcutString("Esc"));
        Assert.AreEqual("Print Screen", ShortcutManager.NormalizeShortcutString("Print Screen"));
    }

    [TestMethod]
    public void ShortcutManager_ConflictDetection_DetectsCollisionsAccurately()
    {
        var sm = ShortcutManager.Instance;
        // Ctrl+C is assigned to Copy by default
        var (hasConflict, conflictName) = sm.CheckConflict("Ctrl + C", "Save");
        Assert.IsTrue(hasConflict);
        Assert.AreEqual("Copiar captura atual", conflictName);

        // Self-conflict is ignored (editing Save to its own current shortcut)
        var (selfConflict, _) = sm.CheckConflict("Ctrl + S", "Save");
        Assert.IsFalse(selfConflict);

        // Unused shortcut has no conflict
        var (noConflict, _) = sm.CheckConflict("Ctrl + Alt + F12", "Save");
        Assert.IsFalse(noConflict);
    }

    [TestMethod]
    public void StepMarker_FormatMarkerText_HandlesAllStyles()
    {
        // Numbers
        Assert.AreEqual("1", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Numbers, 1));
        Assert.AreEqual("42", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Numbers, 42));

        // Uppercase: 1 -> A, 26 -> Z, 27 -> AA
        Assert.AreEqual("A", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Uppercase, 1));
        Assert.AreEqual("B", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Uppercase, 2));
        Assert.AreEqual("Z", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Uppercase, 26));
        Assert.AreEqual("AA", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Uppercase, 27));

        // Lowercase: 1 -> a, 26 -> z, 27 -> aa
        Assert.AreEqual("a", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Lowercase, 1));
        Assert.AreEqual("z", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Lowercase, 26));
        Assert.AreEqual("aa", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Lowercase, 27));

        // Dots: empty string
        Assert.AreEqual("", ShotMind.Core.Models.AnnotationItem.FormatMarkerText(ShotMind.Core.StepMarkerStyle.Dots, 1));
    }

    [TestMethod]
    public void AnnotationItem_Clone_IncludesAllStepMarkerProperties()
    {
        var item = new ShotMind.Core.Models.AnnotationItem
        {
            Type = ShotMind.Core.ToolType.StepMarker,
            StartPoint = new System.Windows.Point(10, 20),
            EndPoint = new System.Windows.Point(10, 20),
            Color = System.Windows.Media.Colors.DeepSkyBlue,
            MarkerStyle = ShotMind.Core.StepMarkerStyle.Uppercase,
            MarkerNumber = 3,
            MarkerText = "C",
            MarkerTextColor = System.Windows.Media.Colors.White,
            MarkerSize = 32.0,
            MarkerHasFill = true,
            MarkerHasBorder = true
        };

        var clone = item.Clone();
        Assert.AreEqual(item.Type, clone.Type);
        Assert.AreEqual(item.StartPoint, clone.StartPoint);
        Assert.AreEqual(item.Color, clone.Color);
        Assert.AreEqual(item.MarkerStyle, clone.MarkerStyle);
        Assert.AreEqual(item.MarkerNumber, clone.MarkerNumber);
        Assert.AreEqual(item.MarkerText, clone.MarkerText);
        Assert.AreEqual(item.MarkerTextColor, clone.MarkerTextColor);
        Assert.AreEqual(item.MarkerSize, clone.MarkerSize);
        Assert.AreEqual(item.MarkerHasFill, clone.MarkerHasFill);
        Assert.AreEqual(item.MarkerHasBorder, clone.MarkerHasBorder);
    }

    [TestMethod]
    public void ExportService_RenderAnnotatedBitmap_RendersStepMarkerWithoutErrors()
    {
        using var baseBmp = new System.Drawing.Bitmap(200, 200);
        using (var g = System.Drawing.Graphics.FromImage(baseBmp))
        {
            g.Clear(System.Drawing.Color.LightGray);
        }

        var annotations = new System.Collections.Generic.List<ShotMind.Core.Models.AnnotationItem>
        {
            new ShotMind.Core.Models.AnnotationItem
            {
                Type = ShotMind.Core.ToolType.StepMarker,
                StartPoint = new System.Windows.Point(50, 50),
                EndPoint = new System.Windows.Point(50, 50),
                Color = System.Windows.Media.Colors.Purple,
                MarkerStyle = ShotMind.Core.StepMarkerStyle.Numbers,
                MarkerNumber = 1,
                MarkerText = "1",
                MarkerTextColor = System.Windows.Media.Colors.White,
                MarkerSize = 28.0,
                MarkerHasFill = true,
                MarkerHasBorder = true
            },
            new ShotMind.Core.Models.AnnotationItem
            {
                Type = ShotMind.Core.ToolType.StepMarker,
                StartPoint = new System.Windows.Point(120, 120),
                EndPoint = new System.Windows.Point(120, 120),
                Color = System.Windows.Media.Colors.DarkOrange,
                MarkerStyle = ShotMind.Core.StepMarkerStyle.Dots,
                MarkerNumber = 2,
                MarkerText = "",
                MarkerTextColor = System.Windows.Media.Colors.White,
                MarkerSize = 24.0,
                MarkerHasFill = true,
                MarkerHasBorder = true
            }
        };

        using var result = ExportService.RenderAnnotatedBitmap(baseBmp, annotations);
        Assert.IsNotNull(result);
        Assert.AreEqual(200, result.Width);
        Assert.AreEqual(200, result.Height);
    }

    [TestMethod]
    public void ThemeService_ApplyTheme_SetsCorrectAccentPrimaryColorForAllThemes()
    {
        string[] themeNames = { "Purple", "Red", "Pink", "Green", "Yellow", "Gray", "Orange", "Blue" };

        foreach (var name in themeNames)
        {
            bool ok = ThemeService.Instance.ApplyTheme(name, persist: false);
            Assert.IsTrue(ok);
            Assert.AreEqual(name, ThemeService.Instance.CurrentThemeName);

            var expected = ThemeService.AllThemes.First(t => t.Name == name);
            Assert.AreEqual(expected.Primary, ThemeService.Instance.CurrentTheme.Primary);

            // Verify Application.Current resources if application context is available
            if (System.Windows.Application.Current?.Resources != null)
            {
                var brush = System.Windows.Application.Current.Resources["AccentPrimaryBrush"] as SolidColorBrush;
                Assert.IsNotNull(brush);
                Assert.AreEqual(expected.Primary, brush.Color);
            }
        }

        // Restore default
        ThemeService.Instance.ApplyTheme("Purple", persist: false);
    }
}
