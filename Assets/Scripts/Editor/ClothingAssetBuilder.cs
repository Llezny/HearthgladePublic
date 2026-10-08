using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Characters;
using Hearthglade.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // The player's own clothes and the first set of wearable items (docs/EQUIPMENT_PLAN.md, phase 5): the PlayerDefault appearance (what the player shows
    // with nothing worn) and the Traveller set, whose items carry the part of the character kit they put on. Updated in place, so guids stay.
    public static class ClothingAssetBuilder
    {
        public const string DefaultAppearancePath = CharacterAssetBuilder.AppearanceFolder + "/PlayerDefault.asset";
        private const string IconFolder = "Assets/Arts/Sprites/ItemIcons/Equipment";
        private const string ItemFolder = "Assets/ScriptableObjects/Items/Equipment";

        private class Piece
        {
            public string Name, Title, Description, Part;
            public ItemType Type;
            public float Cold, Heat, Damage;
        }

        // Protection values are first guesses; the set is warm and sturdy, so it has no heat protection (a lighter set will).
        private static readonly Piece[] Traveller =
        {
            new Piece { Name = "TravellerCap", Title = "Traveller's Cap", Description = "A knitted wool cap with a turned-up band.",
                Part = "Head_Traveller", Type = ItemType.Helmet, Cold = 0.3f, Damage = 0.05f },
            new Piece { Name = "TravellerCoat", Title = "Traveller's Coat", Description = "A long leather coat with a belt and a fur collar.",
                Part = "Torso_Traveller", Type = ItemType.Chestplate, Cold = 0.5f, Damage = 0.15f },
            new Piece { Name = "TravellerBoots", Title = "Traveller's Boots", Description = "Tall dark boots with a turned cuff.",
                Part = "Shoes_Traveller", Type = ItemType.Footwear, Cold = 0.3f, Damage = 0.1f },
        };

        [ MenuItem( "Tools/Agent Tools/Characters/Build player clothes" ) ]
        public static void BuildAll()
        {
            BuildDefaultAppearance();
            foreach( var piece in Traveller )
            {
                BuildItem( piece );
            }
            AssetDatabase.SaveAssets();
            global::Editor.EditorScripts.RefreshItemsDatabase();
            UnityEngine.Debug.Log( $"[ClothingAssetBuilder] PlayerDefault and {Traveller.Length} pieces of clothing are ready" );
        }

        public static CharacterAppearanceSO BuildDefaultAppearance()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<CharacterAppearanceSO>( DefaultAppearancePath );
            if( appearance == null )
            {
                appearance = ScriptableObject.CreateInstance<CharacterAppearanceSO>();
                AssetDatabase.CreateAsset( appearance, DefaultAppearancePath );
            }
            var serialized = new SerializedObject( appearance );
            serialized.FindProperty( "head" ).objectReferenceValue = Part( "Head_Plain" );
            serialized.FindProperty( "torso" ).objectReferenceValue = Part( "Torso_Plain" );
            serialized.FindProperty( "shoes" ).objectReferenceValue = Part( "Shoes_Low" );
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty( appearance );
            return appearance;
        }

        private static CharacterPartSO Part( string name )
        {
            return AssetDatabase.LoadAssetAtPath<CharacterPartSO>( $"{CharacterAssetBuilder.PartFolder}/{name}.asset" );
        }

        private static void BuildItem( Piece piece )
        {
            var path = $"{ItemFolder}/{piece.Name}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>( path );
            if( item == null )
            {
                item = ScriptableObject.CreateInstance<ItemSO>();
                AssetDatabase.CreateAsset( item, path );
                item.ItemRarity = ItemRarity.Common;
            }
            item.icon = StoneToolAssetBuilder.ImportIcon( $"{IconFolder}/{piece.Name}.png" );
            item.itemName = piece.Title;
            item.itemDescription = piece.Description;
            item.itemType = piece.Type;
            item.maxItemsInStack = 1;
            item.WornPart = Part( piece.Part );
            item.ColdProtection = piece.Cold;
            item.HeatProtection = piece.Heat;
            item.DamageProtection = piece.Damage;
            EditorUtility.SetDirty( item );
        }
    }
}
