using System.Collections.Generic;
using System.Linq;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Trade;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Hidden barter values of the items and the data of the first port (docs/EXPLORATION_LOOP_PLAN.md, sections 5 and 6a).
    // The table is a first guess to be tuned by hand in the item assets; the tool only fills values that are still 0.
    public static class PortDataBuilder
    {
        private const string PortFolder = "Assets/Resources/ScriptableObjects/Ports";

        // Tools, equipment and anything not listed stay at 0 (cannot be traded) until a value is decided.
        private static readonly Dictionary<string, int> BaseValues = new Dictionary<string, int>
        {
            // Gathered basics
            { "Stick", 1 }, { "Wood", 2 }, { "Stone", 3 }, { "Herb", 3 }, { "Aster", 4 }, { "Chamomile", 4 }, { "CattailRoot", 3 },
            { "Flax", 4 }, { "Cotton", 5 },
            // Crafted materials
            { "String", 4 }, { "Paper", 6 }, { "Rope", 8 }, { "Bowl", 10 }, { "Pot", 25 }, { "Bandage", 10 },
            // Ores
            { "Iron", 20 }, { "Crystal", 35 }, { "Gold", 45 },
            // Grain, bread, water
            { "Water", 1 }, { "Wheat", 3 }, { "Oats", 3 }, { "Rye", 3 }, { "Rice", 4 }, { "Bread", 12 },
            // Fruit
            { "Apple", 5 }, { "Pear", 6 }, { "Cherry", 6 }, { "Raspberry", 6 }, { "Blueberry", 6 }, { "Cranberry", 7 },
            { "Lingonberry", 6 }, { "Strawberry", 6 }, { "Grapes", 7 }, { "Lemon", 8 }, { "Date", 8 }, { "Watermelon", 10 },
            // Vegetables and the rest of the farm
            { "Tomato", 6 }, { "Chili", 7 }, { "Corn", 6 }, { "Potato", 5 }, { "Cabbage", 5 }, { "Peas", 5 }, { "Beans", 5 },
            { "Carrot", 4 }, { "Turnip", 4 }, { "Sunflower", 5 }, { "BrownMushroom", 5 }, { "OysterMushroom", 7 },
            { "Honeycomb", 14 }, { "FlatFish", 8 }, { "BakedCarrot", 9 },
            // Seeds and saplings
            { "BeanSeed", 8 }, { "BlueberrySeed", 8 }, { "CabbageSeed", 8 }, { "CarrotSeed", 8 }, { "CattailSeed", 8 },
            { "ChiliSeed", 9 }, { "CornSeed", 8 }, { "CottonSeed", 9 }, { "CranberrySeed", 9 }, { "FlaxSeed", 8 },
            { "LingonberrySeed", 8 }, { "OatsSeed", 7 }, { "PeaSeed", 8 }, { "PotatoSeed", 8 }, { "RaspberrySeed", 8 },
            { "RiceSeed", 8 }, { "RyeSeed", 7 }, { "StrawberrySeed", 8 }, { "SunflowerSeed", 8 }, { "TomatoSeed", 8 },
            { "TurnipSeed", 7 }, { "WatermelonSeed", 9 }, { "WheatSeed", 7 }, { "OysterSpawn", 12 },
            { "AppleSapling", 20 }, { "CherrySapling", 22 }, { "PearSapling", 22 }, { "LemonSapling", 26 },
            { "DatePalmSapling", 28 }, { "GrapeCutting", 15 },
        };

        // For batch mode: everything the first port needs besides its island.
        public static void SetUpPortData()
        {
            AssignBaseValues();
            CreateAllPorts();
        }

        [ MenuItem( "Tools/Agent Tools/Ports/Assign base values to items" ) ]
        public static void AssignBaseValues()
        {
            int assigned = 0, kept = 0;
            var found = new HashSet<string>();
            foreach( var item in AllItems() )
            {
                if( !BaseValues.TryGetValue( item.name, out int value ) )
                {
                    continue;
                }
                found.Add( item.name );
                if( item.BaseValue != 0 )
                {
                    kept++;
                    continue;
                }
                item.BaseValue = value;
                EditorUtility.SetDirty( item );
                assigned++;
            }
            AssetDatabase.SaveAssets();
            var missing = BaseValues.Keys.Where( name => !found.Contains( name ) ).ToList();
            UnityEngine.Debug.Log( $"[PortDataBuilder] Base values: {assigned} assigned, {kept} already set" +
                ( missing.Count > 0 ? $"; no item asset named: {string.Join( ", ", missing )}" : "" ) );
        }

        [ MenuItem( "Tools/Agent Tools/Ports/Create port data (Mosshollow, Rimehaven, Dunegate)" ) ]
        public static void CreateAllPorts()
        {
            CreateMosshollowPort();
            CreateRimehavenPort();
            CreateDunegatePort();
        }

        public static void CreateMosshollowPort()
        {
            // The forest port: fruit and vegetables of the temperate zone, mushrooms, herbs and honey; short of stone, ore and prepared food.
            CreatePort( "Mosshollow", 2, "A mossy harbour town of orchardists and herb gatherers. They have plenty of fruit and honey, and too little stone and ore.",
                new[] { new Requirement( "Wood", 2 ) },
                new[]
                {
                    ( "Pear", 8, 0.7f, 0 ), ( "Cherry", 8, 0.7f, 0 ), ( "Raspberry", 8, 0.7f, 0 ), ( "Potato", 10, 0.7f, 0 ),
                    ( "Cabbage", 8, 0.7f, 0 ), ( "Peas", 8, 0.7f, 0 ), ( "OysterMushroom", 6, 0.8f, 0 ), ( "Herb", 10, 0.7f, 0 ),
                    ( "Flax", 8, 0.7f, 0 ), ( "Honeycomb", 4, 1f, 1 ), ( "PearSapling", 2, 1f, 1 ), ( "CherrySapling", 2, 1f, 1 ),
                },
                new[]
                {
                    ( "Stone", 1.6f ), ( "Iron", 1.5f ), ( "Gold", 1.5f ), ( "Crystal", 1.5f ), ( "Bread", 1.5f ), ( "BakedCarrot", 1.5f ),
                } );
        }

        public static void CreateRimehavenPort()
        {
            // The winter port: hardy grain and cold-climate berries, and the ore of the north; hungry for warm crops and prepared food.
            CreatePort( "Rimehaven", 5, "A frosty fishing harbour of hardy farmers and miners. They sell rye, berries and ore, and pay well for anything warm.",
                new[] { new Requirement( "Wood", 3 ) },
                new[]
                {
                    ( "Rye", 10, 0.7f, 0 ), ( "Oats", 10, 0.7f, 0 ), ( "Turnip", 10, 0.7f, 0 ), ( "Blueberry", 8, 0.7f, 0 ),
                    ( "Cranberry", 8, 0.7f, 0 ), ( "Lingonberry", 8, 0.7f, 0 ), ( "Iron", 8, 0.9f, 0 ), ( "Crystal", 4, 1f, 1 ),
                    ( "RyeSeed", 3, 1f, 1 ), ( "OatsSeed", 3, 1f, 1 ), ( "LingonberrySeed", 3, 1f, 1 ),
                },
                new[]
                {
                    ( "Tomato", 1.6f ), ( "Corn", 1.6f ), ( "Chili", 1.6f ), ( "Lemon", 1.6f ), ( "Watermelon", 1.6f ),
                    ( "Bread", 1.5f ), ( "Cotton", 1.5f ), ( "Flax", 1.5f ), ( "Rope", 1.5f ), ( "BakedCarrot", 1.5f ),
                } );
        }

        public static void CreateDunegatePort()
        {
            // The desert port: warm crops and gold; short of wood and water, and of the crops of wet ground.
            CreatePort( "Dunegate", 7, "A sun-baked trading post at the edge of the dunes. They have warm crops and gold to spare, and are short of wood and water.",
                new[] { new Requirement( "Wood", 4 ) },
                new[]
                {
                    ( "Tomato", 10, 0.7f, 0 ), ( "Corn", 10, 0.7f, 0 ), ( "Chili", 8, 0.7f, 0 ), ( "Lemon", 8, 0.7f, 0 ),
                    ( "Date", 8, 0.7f, 0 ), ( "Watermelon", 6, 0.7f, 0 ), ( "Sunflower", 8, 0.7f, 0 ), ( "Cotton", 8, 0.7f, 0 ),
                    ( "Gold", 6, 0.9f, 0 ), ( "ChiliSeed", 3, 1f, 1 ), ( "DatePalmSapling", 2, 1f, 1 ), ( "LemonSapling", 2, 1f, 1 ),
                    ( "CornSeed", 3, 1f, 1 ),
                },
                new[]
                {
                    ( "Wood", 1.8f ), ( "Water", 1.6f ), ( "Rice", 1.5f ), ( "Cranberry", 1.5f ), ( "OysterMushroom", 1.5f ),
                    ( "BrownMushroom", 1.5f ), ( "Bread", 1.5f ),
                } );
        }

        // Creates the PortSO of a port unless it exists. Offers are (item, stock, price against the base value, relation level), wants (item, demand).
        private static void CreatePort( string name, int unlockAfter, string description, Requirement[] travelCost,
            ( string item, int stock, float price, int level )[] offers, ( string item, float demand )[] wants )
        {
            if( !AssetDatabase.IsValidFolder( PortFolder ) )
            {
                AssetDatabase.CreateFolder( "Assets/Resources/ScriptableObjects", "Ports" );
            }
            string path = $"{PortFolder}/{name}.asset";
            if( AssetDatabase.LoadAssetAtPath<PortSO>( path ) != null )
            {
                UnityEngine.Debug.Log( $"[PortDataBuilder] {name} port data already exists, left as it is" );
                return;
            }

            var items = AllItems().ToDictionary( item => item.name );
            var port = ScriptableObject.CreateInstance<PortSO>();
            port.displayName = name;
            port.mapName = name;
            port.unlockAfterExpeditions = unlockAfter;
            port.description = description;
            port.travelCost.AddRange( travelCost );
            var missing = new List<string>();
            foreach( var ( itemName, stock, price, level ) in offers )
            {
                if( items.TryGetValue( itemName, out var item ) )
                {
                    port.offers.Add( new PortSO.Offer { item = item, stock = stock, priceMultiplier = price, minRelationLevel = level } );
                }
                else
                {
                    missing.Add( itemName );
                }
            }
            foreach( var ( itemName, demand ) in wants )
            {
                if( items.TryGetValue( itemName, out var item ) )
                {
                    port.wants.Add( new PortSO.Want { item = item, demand = demand } );
                }
                else
                {
                    missing.Add( itemName );
                }
            }

            AssetDatabase.CreateAsset( port, path );
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log( $"[PortDataBuilder] Created {path}: {port.offers.Count} offers, {port.wants.Count} wants" +
                ( missing.Count > 0 ? $"; no item asset named: {string.Join( ", ", missing )}" : "" ) );
        }

        private static IEnumerable<ItemSO> AllItems()
        {
            return AssetDatabase.FindAssets( "t:ItemSO", new[] { "Assets/ScriptableObjects/Items" } )
                .Select( guid => AssetDatabase.LoadAssetAtPath<ItemSO>( AssetDatabase.GUIDToAssetPath( guid ) ) )
                .Where( item => item != null );
        }
    }
}
