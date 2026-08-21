using System.Windows;
using System.Windows.Media;
using ChurchBooks.App.Models;

namespace ChurchBooks.App.Services;

public static class ThemeManager
{
    private sealed record Palette(
        Color Window,
        Color Surface,
        Color SurfaceMuted,
        Color SurfaceRaised,
        Color Shell,
        Color ShellMuted,
        Color Input,
        Color Header,
        Color Border,
        Color BorderStrong,
        Color TextPrimary,
        Color TextSecondary,
        Color TextDisabled,
        Color Accent,
        Color AccentHover,
        Color AccentSoft,
        Color AccentContrast,
        Color Success,
        Color SuccessSoft,
        Color Warning,
        Color WarningSoft,
        Color Danger,
        Color DangerSoft,
        Color Teal,
        Color TealSoft,
        Color Purple,
        Color PurpleSoft,
        Color Selection,
        Color ScrollTrack,
        Color ScrollThumb);

    public static void Apply(AppearanceTheme theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        var palette = theme switch
        {
            AppearanceTheme.ClassicWhite => new Palette(
                FromHex("#F5F7FA"), FromHex("#FFFFFF"), FromHex("#F8FAFC"), FromHex("#FFFFFF"),
                FromHex("#FFFFFF"), FromHex("#F8FAFC"), FromHex("#FFFFFF"), FromHex("#F2F5F9"),
                FromHex("#DDE4EC"), FromHex("#C9D3DF"), FromHex("#122033"), FromHex("#607086"), FromHex("#96A3B3"),
                FromHex("#126FE5"), FromHex("#0B62CE"), FromHex("#E9F2FF"), FromHex("#FFFFFF"),
                FromHex("#108A5F"), FromHex("#E8F7F0"), FromHex("#B96A0B"), FromHex("#FFF2E2"),
                FromHex("#BE3345"), FromHex("#FDECEF"), FromHex("#0F9494"), FromHex("#E7F7F7"),
                FromHex("#7654D6"), FromHex("#F0ECFF"), FromHex("#DDEBFF"), FromHex("#EEF2F7"), FromHex("#9AA8BA")),
            AppearanceTheme.ChurchBooksDark => new Palette(
                FromHex("#0F172A"), FromHex("#162033"), FromHex("#1D2A3D"), FromHex("#223047"),
                FromHex("#111827"), FromHex("#182234"), FromHex("#121C2B"), FromHex("#1A2638"),
                FromHex("#31415A"), FromHex("#42536D"), FromHex("#F4F7FB"), FromHex("#AAB4C3"), FromHex("#6F8099"),
                FromHex("#5EA1FF"), FromHex("#7CB3FF"), FromHex("#203A5F"), FromHex("#07111F"),
                FromHex("#45C58B"), FromHex("#173B31"), FromHex("#F0A84A"), FromHex("#4A351E"),
                FromHex("#F16D7A"), FromHex("#4B2630"), FromHex("#4CC8C8"), FromHex("#193E42"),
                FromHex("#B69AF7"), FromHex("#352B55"), FromHex("#274D79"), FromHex("#111A29"), FromHex("#53647C")),
            _ => new Palette(
                FromHex("#F7F7F3"), FromHex("#FFFEFB"), FromHex("#F5F4EF"), FromHex("#FFFFFF"),
                FromHex("#FFFEFB"), FromHex("#F5F4EF"), FromHex("#FFFFFF"), FromHex("#F4F3EE"),
                FromHex("#E4E2DA"), FromHex("#D2D0C7"), FromHex("#172033"), FromHex("#667085"), FromHex("#98A2B3"),
                FromHex("#1677E8"), FromHex("#0D68D4"), FromHex("#EAF3FF"), FromHex("#FFFFFF"),
                FromHex("#149A68"), FromHex("#E8F8F0"), FromHex("#C87514"), FromHex("#FFF4E7"),
                FromHex("#C43C4A"), FromHex("#FFF0F2"), FromHex("#0C9A9A"), FromHex("#E9F8F8"),
                FromHex("#7955D8"), FromHex("#F2ECFF"), FromHex("#DCEBFF"), FromHex("#F0F1EC"), FromHex("#A6AAA6"))
        };

        SetBrush("WindowBrush", palette.Window);
        SetBrush("SurfaceBrush", palette.Surface);
        SetBrush("SurfaceMutedBrush", palette.SurfaceMuted);
        SetBrush("SurfaceRaisedBrush", palette.SurfaceRaised);
        SetBrush("ShellBrush", palette.Shell);
        SetBrush("ShellMutedBrush", palette.ShellMuted);
        SetBrush("InputBrush", palette.Input);
        SetBrush("HeaderBrush", palette.Header);
        SetBrush("BorderBrush", palette.Border);
        SetBrush("BorderStrongBrush", palette.BorderStrong);
        SetBrush("TextPrimaryBrush", palette.TextPrimary);
        SetBrush("TextSecondaryBrush", palette.TextSecondary);
        SetBrush("TextDisabledBrush", palette.TextDisabled);
        SetBrush("AccentBrush", palette.Accent);
        SetBrush("AccentHoverBrush", palette.AccentHover);
        SetBrush("AccentSoftBrush", palette.AccentSoft);
        SetBrush("AccentContrastBrush", palette.AccentContrast);
        SetBrush("SuccessBrush", palette.Success);
        SetBrush("SuccessSoftBrush", palette.SuccessSoft);
        SetBrush("WarningBrush", palette.Warning);
        SetBrush("WarningSoftBrush", palette.WarningSoft);
        SetBrush("DangerBrush", palette.Danger);
        SetBrush("DangerSoftBrush", palette.DangerSoft);
        SetBrush("TealBrush", palette.Teal);
        SetBrush("TealSoftBrush", palette.TealSoft);
        SetBrush("PurpleBrush", palette.Purple);
        SetBrush("PurpleSoftBrush", palette.PurpleSoft);
        SetBrush("SelectionBrush", palette.Selection);
        SetBrush("ScrollTrackBrush", palette.ScrollTrack);
        SetBrush("ScrollThumbBrush", palette.ScrollThumb);
    }

    private static void SetBrush(string key, Color color)
    {
        var resources = Application.Current.Resources;
        var found = SetBrushRecursive(resources, key, color);
        if (!found)
        {
            resources[key] = NewBrush(color);
        }
    }

    // ThemeTokens.xaml is a merged dictionary. Updating only Application.Resources[key]
    // can create a shadow resource while styles inside another merged dictionary keep
    // resolving the original light brush. Update every dictionary that actually owns
    // the key so direct element resources, shared styles, templates, and inherited
    // foregrounds all switch together.
    private static bool SetBrushRecursive(ResourceDictionary dictionary, string key, Color color)
    {
        var found = false;

        if (ContainsLocalKey(dictionary, key))
        {
            found = true;
            if (dictionary[key] is SolidColorBrush brush && !brush.IsFrozen)
            {
                brush.Color = color;
            }
            else
            {
                dictionary[key] = NewBrush(color);
            }
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (SetBrushRecursive(merged, key, color))
            {
                found = true;
            }
        }

        return found;
    }

    private static bool ContainsLocalKey(ResourceDictionary dictionary, string key)
    {
        foreach (var existingKey in dictionary.Keys)
        {
            if (Equals(existingKey, key))
            {
                return true;
            }
        }

        return false;
    }

    private static SolidColorBrush NewBrush(Color color) => new(color);

    private static Color FromHex(string value) => (Color)ColorConverter.ConvertFromString(value)!;
}
