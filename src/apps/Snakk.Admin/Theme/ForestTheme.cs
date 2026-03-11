namespace Snakk.Admin.Theme;

using MudBlazor;

public static class ForestTheme
{
    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            // Primary — forest dark green (navbar, buttons, active states)
            Primary          = "#1F7A5A",
            PrimaryDarken    = "#145A41",
            PrimaryLighten   = "#4CAF7D",
            PrimaryContrastText = "#FFFFFF",

            // Secondary — medium green (chips, secondary actions)
            Secondary        = "#4CAF7D",
            SecondaryDarken  = "#1F7A5A",
            SecondaryLighten = "#80C9A0",
            SecondaryContrastText = "#FFFFFF",

            // Semantic colors
            Success          = "#4CAF7D",
            SuccessContrastText = "#FFFFFF",
            Warning          = "#E76F51",
            WarningContrastText = "#FFFFFF",
            Error            = "#C0392B",
            ErrorContrastText = "#FFFFFF",
            Info             = "#2980B9",
            InfoContrastText = "#FFFFFF",

            // Surfaces & backgrounds
            Background       = "#F1FAF5",
            BackgroundGray   = "#E8F6EF",
            Surface          = "#FFFFFF",

            // AppBar
            AppbarBackground = "#1F7A5A",
            AppbarText       = "rgba(255,255,255,0.92)",

            // Drawer / sidebar
            DrawerBackground = "#F1FAF5",
            DrawerText       = "#1B2B24",
            DrawerIcon       = "#5E6E66",

            // Text
            TextPrimary      = "#1B2B24",
            TextSecondary    = "#5E6E66",
            TextDisabled     = "rgba(27,43,36,0.38)",

            // Lines & dividers
            Divider          = "#DDE5DD",
            DividerLight     = "#E8EFE8",

            // Overlay & action
            ActionDefault    = "#5E6E66",
            ActionDisabled   = "rgba(27,43,36,0.26)",
            ActionDisabledBackground = "rgba(27,43,36,0.12)",

            OverlayLight     = "rgba(27,43,36,0.12)",
            OverlayDark      = "rgba(27,43,36,0.50)",

            // Table zebra stripe
            TableStriped     = "rgba(76,175,125,0.04)",
            TableHover       = "rgba(31,122,90,0.06)",
        },

        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Roboto", "system-ui", "sans-serif"]
            },
            H5 = new H5Typography
            {
                FontWeight = "600"
            },
            H6 = new H6Typography
            {
                FontWeight = "600"
            }
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
            DrawerWidthLeft     = "260px",
        }
    };
}
