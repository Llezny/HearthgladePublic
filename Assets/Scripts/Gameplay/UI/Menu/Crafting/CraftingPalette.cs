using UnityEngine;

namespace Hearthglade.Gameplay.UI.Menu.Crafting
{
    // Colours of the crafting screen, sampled from the light Figma design (warm cream surfaces, brown ink,
    // amber / emerald / rose accents).
    public static class CraftingPalette
    {
        // text, darkest to lightest
        public static readonly Color Ink900 = Hex( "25190a" );   // titles, item names
        public static readonly Color OnAmber = Hex( "422500" );  // text and icons on an amber fill
        public static readonly Color Ink700 = Hex( "534434" );
        public static readonly Color Ink600 = Hex( "6b5c4c" );   // descriptions, labels
        public static readonly Color Ink500 = Hex( "8d7c6b" );   // muted
        public static readonly Color Ink400 = Hex( "938171" );   // disabled

        // surfaces
        public static readonly Color Surface = Hex( "ffffff" );      // recipe cards
        public static readonly Color SurfaceWarm = Hex( "fffaf4" );  // footer, selected card, quantity box
        public static readonly Color Cream = Hex( "fff1e5" );        // icon boxes, step buttons
        public static readonly Color Header = Hex( "fff6ee" );
        public static readonly Color TabsBar = Hex( "fef8eb" );
        public static readonly Color Sand = Hex( "f5ebe0" );         // disabled button
        public static readonly Color Tan = Hex( "ede0d2" );          // "x1" badge
        public static readonly Color Border = Hex( "f0e4d8" );

        public static readonly Color Amber800 = Hex( "92400e" );
        public static readonly Color Amber600 = Hex( "d97706" );
        public static readonly Color Amber500 = Hex( "f59e0b" );
        public static readonly Color AmberTab = Hex( "f7ae18" );     // active tab (the design's gradient midpoint)
        public static readonly Color Amber400 = Hex( "fbbf24" );
        public static readonly Color Amber300 = Hex( "fcd34d" );

        public static readonly Color Emerald700 = Hex( "047857" );
        public static readonly Color Emerald600 = Hex( "059669" );
        public static readonly Color Emerald500 = Hex( "10b981" );
        public static readonly Color Emerald200 = Hex( "a7f3d0" );
        public static readonly Color Emerald50 = Hex( "ecfdf5" );

        public static readonly Color Rose600 = Hex( "e11d48" );
        public static readonly Color Rose500 = Hex( "f43f5e" );
        public static readonly Color Rose300 = Hex( "fda4af" );
        public static readonly Color Rose50 = Hex( "fff1f2" );

        public static readonly Color Cyan600 = Hex( "0891b2" );

        public static Color WithAlpha( Color color, float alpha ) {
            color.a = alpha;
            return color;
        }

        public static string ToHex( Color color ) {
            return ColorUtility.ToHtmlStringRGB( color );
        }

        public static Color Hex( string hex ) {
            ColorUtility.TryParseHtmlString( "#" + hex, out var color );
            return color;
        }
    }
}
