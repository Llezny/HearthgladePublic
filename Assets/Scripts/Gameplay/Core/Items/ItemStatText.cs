using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hearthglade.Core.Items
{
    // The stats of an item as short lines of text for the screens that show them (crafting card, equipment panel).
    public static class ItemStatText
    {
        public static IReadOnlyList<string> Lines( ItemDefinition item ) {
            var lines = new List<string>();
            if( item == null ) {
                return lines;
            }
            var stats = item.Stats;
            if( stats.ChopSpeed > 0f ) {
                lines.Add( "Chopping x" + Number( stats.ChopSpeed ) );
            }
            if( stats.MineSpeed > 0f ) {
                lines.Add( "Mining x" + Number( stats.MineSpeed ) );
            }
            if( stats.HarvestSpeed > 0f ) {
                lines.Add( "Harvesting x" + Number( stats.HarvestSpeed ) );
            }
            if( stats.AttackPower > 0f ) {
                lines.Add( "Attack " + Number( stats.AttackPower ) );
            }
            var protection = item.Protection;
            if( protection.Cold > 0f ) {
                lines.Add( "Cold protection " + Number( protection.Cold ) );
            }
            if( protection.Heat > 0f ) {
                lines.Add( "Heat protection " + Number( protection.Heat ) );
            }
            if( protection.Damage > 0f ) {
                lines.Add( "Damage -" + Percent( protection.Damage ) );
            }
            return lines;
        }

        // What the whole outfit shields from, on one line; the damage share is the capped one that really applies.
        public static string Summary( Protection protection ) {
            return "Cold " + Number( protection.Cold ) + "   Heat " + Number( protection.Heat ) + "   Damage -" + Percent( protection.DamageReduction );
        }

        private static string Number( float value ) {
            return value.ToString( "0.##", CultureInfo.InvariantCulture );
        }

        private static string Percent( float fraction ) {
            return Math.Round( fraction * 100f ).ToString( "0", CultureInfo.InvariantCulture ) + "%";
        }
    }
}
