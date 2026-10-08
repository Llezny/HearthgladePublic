using System.IO;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Turns the stone tools of Tools/Agent Tools/Blender/stone_tools.py (FBX + icon per tool) into game assets (docs/EQUIPMENT_PLAN.md, phase 4):
    // an in-hand prefab and an ItemSO per tool. Existing assets are updated in place, so their guids stay.
    public static class StoneToolAssetBuilder
    {
        private const string ModelFolder = "Assets/Arts/Models/Items";
        private const string IconFolder = "Assets/Arts/Sprites/ItemIcons/Equipment";
        private const string PrefabFolder = "Assets/_Prefabs/Items";
        private const string ItemFolder = "Assets/ScriptableObjects/Items/Equipment";
        private const string MaterialPath = "Assets/3rd-Party/BrokenVector/LowPolyTreePack/Materials/Normal.mat";

        private class Tool
        {
            public string Name, Title, Description;
            public ItemType Type;
            public ToolGroup Group;
            public float Chop, Harvest, Mine, Attack, Durability;
        }

        // Speeds are multipliers of bare hands (1.0); see docs/EQUIPMENT_PLAN.md.
        private static readonly Tool[] Tools =
        {
            new Tool { Name = "StoneAxe", Title = "Stone Axe", Description = "A flint head lashed to a wooden haft. Cuts down trees.",
                Type = ItemType.Tool, Group = ToolGroup.Axe, Chop = 2f, Attack = 3f, Durability = 100f },
            new Tool { Name = "StonePickaxe", Title = "Stone Pickaxe", Description = "A pointed stone on a stout haft. Breaks rock and ore.",
                Type = ItemType.Tool, Group = ToolGroup.Pickaxe, Mine = 2f, Attack = 3f, Durability = 100f },
            new Tool { Name = "StoneSpear", Title = "Stone Spear", Description = "A long shaft with a flint point. Made for thrusting.",
                Type = ItemType.Weapon, Group = ToolGroup.Spear, Attack = 6f, Durability = 60f },
            new Tool { Name = "StoneSickle", Title = "Stone Sickle", Description = "A curved flint blade. Gathers plants faster than bare hands.",
                Type = ItemType.Tool, Group = ToolGroup.Sickle, Harvest = 2f, Attack = 1f, Durability = 80f },
        };

        [ MenuItem( "Tools/Agent Tools/Items/Build stone tools" ) ]
        public static void BuildAll()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>( MaterialPath );
            Directory.CreateDirectory( PrefabFolder );
            foreach( var tool in Tools )
            {
                var icon = ImportIcon( $"{IconFolder}/{tool.Name}.png" );
                var prefab = BuildPrefab( tool, material );
                BuildItem( tool, icon, prefab );
            }
            RemoveSuperseded();
            AssetDatabase.SaveAssets();
            global::Editor.EditorScripts.RefreshItemsDatabase();
            UnityEngine.Debug.Log( $"[StoneToolAssetBuilder] {Tools.Length} stone tools are ready" );
        }

        internal static Sprite ImportIcon( string path )
        {
            AssetDatabase.ImportAsset( path, ImportAssetOptions.ForceSynchronousImport );
            var importer = ( TextureImporter ) AssetImporter.GetAtPath( path );
            // The same settings as the other item icons.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>( path );
        }

        // The prefab is the model alone, with the origin at the grip and the shaft along +Y, the edge or point along +Z: the hand socket
        // of the character is what turns it into the right pose.
        private static GameObject BuildPrefab( Tool tool, Material material )
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>( $"{ModelFolder}/{tool.Name}.fbx" );
            var root = new GameObject( tool.Name );
            var instance = ( GameObject ) PrefabUtility.InstantiatePrefab( model, root.transform );
            instance.name = "Model";
            foreach( var renderer in instance.GetComponentsInChildren<MeshRenderer>() )
            {
                renderer.sharedMaterial = material;
            }
            var bounds = new Bounds( Vector3.zero, Vector3.zero );
            foreach( var renderer in instance.GetComponentsInChildren<MeshRenderer>() )
            {
                bounds.Encapsulate( renderer.bounds );
            }
            UnityEngine.Debug.Log( $"[StoneToolAssetBuilder] {tool.Name} bounds {bounds.min:F3} .. {bounds.max:F3}" );
            var path = $"{PrefabFolder}/{tool.Name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset( root, path );
            Object.DestroyImmediate( root );
            return prefab;
        }

        private static void BuildItem( Tool tool, Sprite icon, GameObject prefab )
        {
            var path = $"{ItemFolder}/{tool.Name}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>( path );
            if( item == null )
            {
                item = ScriptableObject.CreateInstance<ItemSO>();
                AssetDatabase.CreateAsset( item, path );
                item.ItemRarity = ItemRarity.Common;
            }
            item.icon = icon;
            item.itemName = tool.Title;
            item.itemDescription = tool.Description;
            item.itemType = tool.Type;
            item.maxItemsInStack = 1;
            item.GameObject = prefab;
            item.HasDurability = true;
            item.MaxDurability = tool.Durability;
            item.Damage = Mathf.RoundToInt( tool.Attack );
            item.ToolGroup = tool.Group;
            item.ChopSpeed = tool.Chop;
            item.HarvestSpeed = tool.Harvest;
            item.MineSpeed = tool.Mine;
            item.AttackPower = tool.Attack;
            EditorUtility.SetDirty( item );
        }

        // The first models of the axe and the pickaxe and the first axe icon are replaced; nothing else uses them.
        private static void RemoveSuperseded()
        {
            foreach( var path in new[]
            {
                $"{ModelFolder}/axe.fbx",
                $"{ModelFolder}/pickaxe.fbx",
                $"{PrefabFolder}/Axe.prefab",
                $"{PrefabFolder}/Pickaxe.prefab",
                $"{IconFolder}/stone-axe.png",
            } )
            {
                if( AssetDatabase.LoadMainAssetAtPath( path ) != null )
                {
                    AssetDatabase.DeleteAsset( path );
                }
            }
        }
    }
}
