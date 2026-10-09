using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hearthglade.Core.Items;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Housing;
using Hearthglade.Gameplay.Items.BuildableItems;
using Hearthglade.Gameplay.Map;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // The house on the home island and the room inside it (docs/HOME_ISLAND_PLAN.md, phase 3): imports the models of
    // Tools/Agent Tools/Blender/home_interior.py, builds the prefabs HomeHouse (the forest house with a door to tap) and HomeInterior (the
    // diorama room), the furniture the player can build in it (items, prefabs, recipes), and places the house with an anchored point of interest in the config of Home. The POI only shows up in new games.
    // The house has three levels (phase 6): a look and a room for each, and the upgrades to them are items with recipes. The room grows to the
    // south and west, so its north-east corner stays where it was and the furniture keeps its cells.
    // Safe to run again: prefabs are rebuilt in place (keeping their guids), the POI is added to the config once.
    public static class HomeHouseBuilder
    {
        private const string ModelFolder = "Assets/Arts/Models/Environment/Home";
        private const string ForestModelFolder = "Assets/Arts/Models/Environment/HarborForest";
        private const string PrefabFolder = "Assets/_Prefabs/Environment/Home";
        private const string MaterialPath = "Assets/Arts/Materials/Environment/HarborForest.mat";
        private const string PoiPath = "Assets/ScriptableObjects/Map/Poi/Home_House.asset";
        private const string ItemFolder = "Assets/ScriptableObjects/Map/Buildings";
        private const string IconFolder = "Assets/Arts/Sprites/ItemIcons/Buildable";
        private const string RecipesPath = "Assets/Resources/JSON/BuildingsRecipies.JSON";
        private const string HomeMapPath = "Assets/Resources/ScriptableObjects/Maps/Home.asset";

        // Cells from the player's start to the house (x east, z north). Checked in the game by HouseShot.
        private static readonly Vector2Int HouseOffset = new Vector2Int( 9, 3 );

        // The room, the same numbers as home_interior.py: 8 x 6 cells, the door in the north wall.
        private const float Cell = 0.3675f;
        private const float RoomWidth = 8 * Cell;
        private const float RoomDepth = 6 * Cell;
        private const float DoorX = -0.7f;

        // What is part of the house itself (the player cannot move it): metres from the middle of the floor (x east, z north), yaw as the
        // Blender script writes it. The table, the stools and the rug are furniture the player builds (see Furniture below).
        private struct Fixture
        {
            public string Name, Model;
            public float X, Z, Yaw;
        }

        private static Fixture Piece( string name, string model, float x, float z, float yaw = 0f ) =>
            new Fixture { Name = name, Model = model, X = x, Z = z, Yaw = yaw };

        private static readonly Fixture[] Layout =
        {
            Piece( "Bed", "HomeBed", RoomWidth / 2 - 0.45f, RoomDepth / 2 - 0.275f ),
            Piece( "Hearth", "HomeHearth", -0.1f, RoomDepth / 2 - 0.125f ),
            Piece( "Shelf", "HomeShelf", RoomWidth / 2 - 0.11f, -0.55f, 90f ),
        };

        // Furniture the player builds in the house (docs/HOME_ISLAND_PLAN.md, phase 5): an item, a prefab and a recipe each. The footprint is
        // in cells; the height only sizes the box that is tapped to pick the piece up again.
        private struct FurnitureSpec
        {
            public string Name, Model, Title, Description, Material;
            public int Width, Depth, Cost;
            public float Height;
            public bool Flat;
        }

        private static readonly FurnitureSpec[] Furniture =
        {
            new FurnitureSpec { Name = "Stool", Model = "HomeStool", Title = "Stool", Description = "A simple three-legged stool.", Width = 1, Depth = 1, Height = 0.14f, Material = "Wood", Cost = 2 },
            new FurnitureSpec { Name = "Table", Model = "HomeTable", Title = "Table", Description = "A sturdy table to eat at.", Width = 2, Depth = 1, Height = 0.22f, Material = "Wood", Cost = 6 },
            new FurnitureSpec { Name = "Rug", Model = "HomeRug", Title = "Rug", Description = "A warm rug woven from cotton.", Width = 3, Depth = 2, Height = 0.03f, Material = "Cotton", Cost = 4, Flat = true },
        };

        // The three levels: the size of the room in cells (the north-east corner is the same in all), the look of the house outside (a
        // forest house model with the spec of HarborForestPropsBuilder), where its door is, and what the upgrade to the level costs.
        // A level with its own model (home_house.py) is drawn at the size it has in the game and sits in ModelFolder; the others still use a
        // forest house. DoorCentre/DoorSize (the tap box of the door leaf, in metres) are only set for the models of our own.
        private struct LevelSpec
        {
            public int Cols, Rows;
            public string Model, Title, Description;
            public bool Own;
            public float Scale;
            public Vector3 BodyCentre, BodySize, Door, DoorCentre, DoorSize;
            public (Vector3 centre, Vector3 size)[] Extra; // more solid boxes: what the body box does not cover (porch posts, wood pile)
            public (string item, int count)[] Price;
        }

        private static readonly LevelSpec[] Levels =
        {
            // The cabin: timber on a fieldstone foundation with a porch and a chimney on the west gable. The door is in the front wall under the porch.
            new LevelSpec { Cols = 8, Rows = 6, Model = "HomeHouse1", Own = true, Scale = 1f, BodyCentre = new Vector3( -0.06f, 0.35f, 0f ), BodySize = new Vector3( 1.48f, 0.7f, 0.88f ), Door = new Vector3( 0f, 0f, -0.41f ),
                DoorCentre = new Vector3( 0f, 0.29f, 0f ), DoorSize = new Vector3( 0.34f, 0.38f, 0.16f ),
                Extra = new[] { ( new Vector3( -0.255f, 0.24f, -0.70f ), new Vector3( 0.1f, 0.48f, 0.1f ) ), ( new Vector3( 0.255f, 0.24f, -0.70f ), new Vector3( 0.1f, 0.48f, 0.1f ) ),
                    ( new Vector3( 0.77f, 0.07f, -0.2f ), new Vector3( 0.24f, 0.14f, 0.28f ) ) } },
            new LevelSpec { Cols = 10, Rows = 7, Model = "ForestHouseB", Scale = 1.8f, BodyCentre = new Vector3( 0f, 0.22f, 0f ), BodySize = new Vector3( 0.54f, 0.44f, 0.58f ), Door = new Vector3( -0.126f, 0f, -0.55f ),
                Title = "Two-storey house", Description = "A taller house with a bigger room and a second window.", Price = new[] { ( "Wood", 30 ), ( "Stone", 15 ), ( "Rope", 4 ) } },
            new LevelSpec { Cols = 12, Rows = 8, Model = "ForestHouseC", Scale = 1.8f, BodyCentre = new Vector3( 0f, 0.2f, -0.095f ), BodySize = new Vector3( 0.8f, 0.4f, 0.7f ), Door = new Vector3( 0f, 0f, -0.88f ),
                Title = "Longhouse", Description = "A long house with a porch, a big room and three windows.", Price = new[] { ( "Wood", 60 ), ( "Stone", 30 ), ( "Iron", 10 ), ( "Crystal", 4 ) } },
        };

        [ MenuItem( "Tools/Agent Tools/Home/Build house and interior" ) ]
        public static void BuildAll()
        {
            if( !AssetDatabase.IsValidFolder( PrefabFolder ) )
            {
                AssetDatabase.CreateFolder( "Assets/_Prefabs/Environment", "Home" );
            }
            AssetDatabase.Refresh();
            HarborAssetBuilder.ImportModels( ModelFolder );
            var material = AssetDatabase.LoadAssetAtPath<Material>( MaterialPath );
            if( material == null )
            {
                UnityEngine.Debug.LogError( $"[HomeHouseBuilder] {MaterialPath} is missing (run Tools > Agent Tools > Ports > Build Mosshollow forest props first)" );
                return;
            }

            var interiors = new HouseInterior[ Levels.Length ];
            for( int level = 1; level <= Levels.Length; level++ )
            {
                interiors[ level - 1 ] = BuildInterior( level, material );
            }
            DeleteOldAssets();
            BuildFurniture( material );
            BuildUpgrades();
            var house = BuildHouse( material, interiors );
            var poi = EnsurePoi( house );
            AddToHome( poi );
            AssetDatabase.SaveAssets();
            global::Editor.EditorScripts.RefreshItemsDatabase();
            UnityEngine.Debug.Log( "[HomeHouseBuilder] HomeHouse and its three rooms are ready, Home_House is on the config of Home" );
        }

        // ---- the room ----

        // Assets of the first version, one room and one shell for all.
        private static void DeleteOldAssets()
        {
            AssetDatabase.DeleteAsset( $"{PrefabFolder}/HomeInterior.prefab" );
            AssetDatabase.DeleteAsset( $"{ModelFolder}/HomeShell.fbx" );
        }

        private static HouseInterior BuildInterior( int level, Material material )
        {
            var spec = Levels[ level - 1 ];
            float width = spec.Cols * Cell, depth = spec.Rows * Cell;
            // The north-east corner of the floor is fixed (4 cells east and 3 north of the middle of the first room), so a bigger floor
            // has its middle to the south-west of the first one. Everything the player meets (furniture, door, spawn) keeps its place.
            var floorMiddle = new Vector3( 4 * Cell - width / 2, 0f, 3 * Cell - depth / 2 );

            var root = new GameObject( $"HomeInterior{level}" );
            var middle = new GameObject( "FloorCentre" );
            middle.transform.SetParent( root.transform, false );
            middle.transform.localPosition = floorMiddle;
            Model( root.transform, "Shell", $"HomeShell{level}", floorMiddle, 0f );
            Model( root.transform, "Void", "HomeVoid", Vector3.zero, 0f );
            foreach( var piece in Layout )
            {
                Model( root.transform, piece.Name, piece.Model, new Vector3( piece.X, 0f, piece.Z ), piece.Yaw );
            }
            foreach( var renderer in root.GetComponentsInChildren<Renderer>() )
            {
                renderer.sharedMaterial = material;
            }

            // A floor for anything that falls or casts down; the player is kinematic and does not need it.
            var floor = new GameObject( "Floor" );
            floor.transform.SetParent( root.transform, false );
            var floorBox = floor.AddComponent<BoxCollider>();
            floorBox.center = floorMiddle + new Vector3( 0f, -0.05f, 0f );
            floorBox.size = new Vector3( width, 0.1f, depth );
            // The layer the building placer aims its ray at, so that furniture can be put down on this floor.
            floor.layer = LayerMask.NameToLayer( "Block" );

            // The footprints that keep the player out of the furniture come from the models themselves, so the numbers are not repeated here.
            var obstacles = new List<BoxCollider>();
            foreach( var piece in Layout )
            {
                var visual = root.transform.Find( piece.Name );
                var bounds = BoundsOf( visual );
                var obstacle = new GameObject( $"Obstacle{piece.Name}" );
                obstacle.transform.SetParent( root.transform, false );
                var box = obstacle.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = bounds.center;
                box.size = bounds.size;
                obstacles.Add( box );
            }

            // The door in the north wall: its leaf is the tap target, the player appears in front of it looking into the room.
            var exit = new GameObject( "Exit", typeof( HouseExit ) ) { layer = LayerMask.NameToLayer( "Clickable" ) };
            exit.transform.SetParent( root.transform, false );
            exit.transform.localPosition = new Vector3( DoorX, 0.2f, 3 * Cell + 0.02f );
            var exitBox = exit.AddComponent<BoxCollider>();
            exitBox.isTrigger = true;
            exitBox.size = new Vector3( 0.3f, 0.42f, 0.12f );

            var spawn = new GameObject( "SpawnPoint" );
            spawn.transform.SetParent( root.transform, false );
            spawn.transform.localPosition = new Vector3( DoorX, 0f, 3 * Cell - 0.3f );
            spawn.transform.localRotation = Quaternion.Euler( 0f, 180f, 0f );

            // A warm light of the hearth and the window; the sun does not reach in as it does on the island. A bigger room gets a wider reach.
            var lightObject = new GameObject( "Light" );
            lightObject.transform.SetParent( root.transform, false );
            lightObject.transform.localPosition = floorMiddle + new Vector3( -0.15f, 0.6f, 0.1f );
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color( 1f, 0.78f, 0.5f );
            light.intensity = 2.2f;
            light.range = 2.5f + width * 0.45f;
            light.shadows = LightShadows.None;

            var interior = root.AddComponent<HouseInterior>();
            var serialized = new SerializedObject( interior );
            serialized.FindProperty( "spawnPoint" ).objectReferenceValue = spawn.transform;
            serialized.FindProperty( "floorCentre" ).objectReferenceValue = middle.transform;
            serialized.FindProperty( "roomHalfSize" ).vector2Value = new Vector2( width / 2, depth / 2 );
            var list = serialized.FindProperty( "obstacles" );
            list.arraySize = obstacles.Count;
            for( int i = 0; i < obstacles.Count; i++ )
            {
                list.GetArrayElementAtIndex( i ).objectReferenceValue = obstacles[ i ];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset( root, $"{PrefabFolder}/HomeInterior{level}.prefab" );
            Object.DestroyImmediate( root );
            return prefab.GetComponent<HouseInterior>();
        }

        // A model of the room as a child, turned by the model flip of the import (see HarborAssetBuilder) and by its own yaw.
        private static GameObject Model( Transform parent, string name, string model, Vector3 position, float yaw )
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>( $"{ModelFolder}/{model}.fbx" );
            var visual = ( GameObject ) PrefabUtility.InstantiatePrefab( asset, parent );
            visual.name = name;
            visual.transform.localPosition = position;
            visual.transform.localRotation = Quaternion.Euler( 0f, 180f + yaw, 0f );
            visual.transform.localScale = Vector3.one;
            return visual;
        }

        // The bounds of a model in the space of its parent, from the mesh and not from the renderer (which has no bounds before it is drawn).
        private static Bounds BoundsOf( Transform visual )
        {
            var bounds = new Bounds();
            bool first = true;
            foreach( var filter in visual.GetComponentsInChildren<MeshFilter>() )
            {
                var box = filter.sharedMesh.bounds;
                for( int i = 0; i < 8; i++ )
                {
                    var corner = box.center + Vector3.Scale( box.extents, new Vector3( ( i & 1 ) == 0 ? -1 : 1, ( i & 2 ) == 0 ? -1 : 1, ( i & 4 ) == 0 ? -1 : 1 ) );
                    var local = visual.parent.InverseTransformPoint( filter.transform.TransformPoint( corner ) );
                    if( first )
                    {
                        bounds = new Bounds( local, Vector3.zero );
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate( local );
                    }
                }
            }
            return bounds;
        }

        // ---- furniture ----

        private static void BuildFurniture( Material material )
        {
            foreach( var spec in Furniture )
            {
                var prefab = BuildFurniturePrefab( spec, material );
                var icon = ImportIcon( $"{IconFolder}/{spec.Name}Icon.png" );
                EnsureItem( spec, prefab, icon );
            }
            AddRecipes( Furniture.Select( f => ( f.Name, new[] { ( f.Material, f.Cost ) } ) ) );
        }

        private static GameObject BuildFurniturePrefab( FurnitureSpec spec, Material material )
        {
            var root = new GameObject( spec.Model, typeof( HouseFurniture ) ) { layer = LayerMask.NameToLayer( "Clickable" ) };
            var visual = Model( root.transform, "Model", spec.Model, Vector3.zero, 0f );
            foreach( var renderer in visual.GetComponentsInChildren<Renderer>() )
            {
                renderer.sharedMaterial = material;
            }
            // The tap target, a little smaller than the footprint so that two pieces side by side do not share a tap.
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3( spec.Width * Cell - 0.05f, spec.Height, spec.Depth * Cell - 0.05f );
            box.center = new Vector3( 0f, spec.Height * 0.5f, 0f );
            var prefab = PrefabUtility.SaveAsPrefabAsset( root, $"{PrefabFolder}/{spec.Model}.prefab" );
            Object.DestroyImmediate( root );
            return prefab;
        }

        // The icons are drawn by home_interior.py; they are imported like the other item icons.
        private static Sprite ImportIcon( string path )
        {
            AssetDatabase.ImportAsset( path );
            if( AssetImporter.GetAtPath( path ) is TextureImporter importer )
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>( path );
        }

        private static void EnsureItem( FurnitureSpec spec, GameObject prefab, Sprite icon )
        {
            string path = $"{ItemFolder}/{spec.Name}.asset";
            var item = AssetDatabase.LoadAssetAtPath<BuildableItemSO>( path );
            if( item == null )
            {
                item = ScriptableObject.CreateInstance<BuildableItemSO>();
                AssetDatabase.CreateAsset( item, path );
            }
            item.itemType = ItemType.Buildable;
            item.itemName = spec.Title;
            item.itemDescription = spec.Description;
            item.maxItemsInStack = 10; // a piece that was picked up goes into the backpack and stacks with its kind
            item.icon = icon;
            item.buildingPrefab = prefab;
            item.sizeInCells = new Vector3Int( spec.Width, 1, spec.Depth );
            item.category = BuildCategory.Furniture;
            item.place = BuildPlace.Indoors;
            item.lyingFlat = spec.Flat;
            EditorUtility.SetDirty( item );
        }

        // The recipes are a plain JSON list; new entries go at its end, in the shape of the others.
        private static void AddRecipes( IEnumerable<(string name, (string item, int count)[] price)> recipes )
        {
            string text = File.ReadAllText( RecipesPath );
            string newline = text.Contains( "\r\n" ) ? "\r\n" : "\n";
            var added = new List<string>();
            foreach( var recipe in recipes.Where( r => !text.Contains( $"\"craftedItemName\": \"{r.name}\"" ) ) )
            {
                var lines = new List<string> { "  {", "    \"requirements\": [" };
                for( int i = 0; i < recipe.price.Length; i++ )
                {
                    var ( item, count ) = recipe.price[ i ];
                    lines.Add( "      {" );
                    lines.Add( $"        \"requiredItemName\": \"{item}\"," );
                    lines.Add( $"        \"requiredQuantity\": {count}," );
                    lines.Add( "        \"Item\": {" );
                    lines.Add( $"          \"Value\": \"{item}\"," );
                    lines.Add( "          \"IsEmpty\": false" );
                    lines.Add( "        }" );
                    lines.Add( i < recipe.price.Length - 1 ? "      }," : "      }" );
                }
                lines.AddRange( new[]
                {
                    "    ],",
                    $"    \"craftedItemName\": \"{recipe.name}\",",
                    "    \"craftedItemQuantity\": 1,",
                    "    \"isUnlocked\": true,",
                    "    \"craftTime\": 2.0,",
                    "    \"xp\": 0,",
                    "    \"tag\": null,",
                    "    \"clearsGrass\": false,",
                    "    \"grassClearRadius\": 0.0,",
                    "    \"CraftedItem\": {",
                    $"      \"Value\": \"{recipe.name}\",",
                    "      \"IsEmpty\": false",
                    "    }",
                    "  }",
                } );
                added.Add( string.Join( newline, lines ) );
            }
            if( added.Count == 0 )
            {
                return;
            }
            int end = text.LastIndexOf( ']' );
            string head = text.Substring( 0, end ).TrimEnd();
            File.WriteAllText( RecipesPath, head + "," + newline + string.Join( "," + newline, added ) + newline + "]" + text.Substring( end + 1 ) );
            AssetDatabase.ImportAsset( RecipesPath );
        }

        // The upgrades to the second and third level: items that the building menu lists inside the house (HouseUpgradeItemSO).
        private static void BuildUpgrades()
        {
            for( int level = 2; level <= Levels.Length; level++ )
            {
                var spec = Levels[ level - 1 ];
                string name = $"HouseLevel{level}";
                string path = $"{ItemFolder}/{name}.asset";
                var item = AssetDatabase.LoadAssetAtPath<HouseUpgradeItemSO>( path );
                if( item == null )
                {
                    item = ScriptableObject.CreateInstance<HouseUpgradeItemSO>();
                    AssetDatabase.CreateAsset( item, path );
                }
                item.itemType = ItemType.Buildable;
                item.itemName = spec.Title;
                item.itemDescription = spec.Description;
                item.maxItemsInStack = 1;
                item.icon = ImportIcon( $"{IconFolder}/{name}Icon.png" );
                item.buildingPrefab = null;
                item.category = BuildCategory.Furniture;
                item.place = BuildPlace.Indoors;
                item.level = level;
                EditorUtility.SetDirty( item );
            }
            AddRecipes( Enumerable.Range( 2, Levels.Length - 1 ).Select( level => ( $"HouseLevel{level}", Levels[ level - 1 ].Price ) ) );
        }

        // ---- the house ----

        private static GameObject BuildHouse( Material material, HouseInterior[] interiors )
        {
            var root = new GameObject( "HomeHouse", typeof( HouseProp ) );
            var looks = new List<(GameObject root, HouseDoor door)>();
            for( int level = 1; level <= Levels.Length; level++ )
            {
                var spec = Levels[ level - 1 ];
                var look = new GameObject( $"Level{level}" );
                look.transform.SetParent( root.transform, false );
                var model = AssetDatabase.LoadAssetAtPath<GameObject>( $"{( spec.Own ? ModelFolder : ForestModelFolder )}/{spec.Model}.fbx" );
                var visual = ( GameObject ) PrefabUtility.InstantiatePrefab( model, look.transform );
                visual.name = "Model";
                foreach( var renderer in visual.GetComponentsInChildren<Renderer>() )
                {
                    renderer.sharedMaterial = material;
                }
                visual.transform.localRotation = Quaternion.Euler( 0f, 180f, 0f );
                visual.transform.localScale = Vector3.one * spec.Scale;
                var body = look.AddComponent<BoxCollider>();
                body.center = spec.BodyCentre * spec.Scale;
                body.size = spec.BodySize * spec.Scale;
                foreach( var (centre, size) in spec.Extra ?? new (Vector3, Vector3)[ 0 ] )
                {
                    var extra = look.AddComponent<BoxCollider>();
                    extra.center = centre * spec.Scale;
                    extra.size = size * spec.Scale;
                }

                // The arched door is in the front face (-Z). The leaf is the tap target, so it sits on the Clickable layer.
                var door = new GameObject( "Door", typeof( HouseDoor ) ) { layer = LayerMask.NameToLayer( "Clickable" ) };
                door.transform.SetParent( look.transform, false );
                door.transform.localPosition = spec.Door;
                var doorBox = door.AddComponent<BoxCollider>();
                doorBox.isTrigger = true;
                doorBox.center = spec.Own ? spec.DoorCentre : new Vector3( 0f, 0.17f, 0f );
                doorBox.size = spec.Own ? spec.DoorSize : new Vector3( 0.32f, 0.34f, 0.16f );
                var doorComponent = door.GetComponent<HouseDoor>();
                var serializedDoor = new SerializedObject( doorComponent );
                serializedDoor.FindProperty( "interiorPrefab" ).objectReferenceValue = interiors[ level - 1 ];
                serializedDoor.ApplyModifiedPropertiesWithoutUndo();
                looks.Add( ( look, doorComponent ) );
            }
            var serialized = new SerializedObject( root.GetComponent<HouseProp>() );
            // The ground the house body and its porch cover, no more (a tuft is removed when its card touches this, so the bare patch is a bit bigger).
            serialized.FindProperty( "grassCentre" ).vector2Value = new Vector2( -0.05f, -0.15f );
            serialized.FindProperty( "grassHalfSize" ).vector2Value = new Vector2( 0.74f, 0.6f );
            serialized.FindProperty( "grassRadius" ).floatValue = 0f;
            var list = serialized.FindProperty( "levels" );
            list.arraySize = looks.Count;
            for( int i = 0; i < looks.Count; i++ )
            {
                var element = list.GetArrayElementAtIndex( i );
                element.FindPropertyRelative( "root" ).objectReferenceValue = looks[ i ].root;
                element.FindPropertyRelative( "door" ).objectReferenceValue = looks[ i ].door;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset( root, $"{PrefabFolder}/HomeHouse.prefab" );
            Object.DestroyImmediate( root );
            return prefab;
        }

        // ---- the place on the island ----

        private static PoiSO EnsurePoi( GameObject house )
        {
            var poi = AssetDatabase.LoadAssetAtPath<PoiSO>( PoiPath );
            if( poi == null )
            {
                poi = ScriptableObject.CreateInstance<PoiSO>();
                AssetDatabase.CreateAsset( poi, PoiPath );
            }
            poi.anchored = true;
            poi.anchorOffset = HouseOffset;
            poi.anchorRotation = 0f;
            poi.clearRadius = 5f; // the biggest house (the longhouse with its porch) and the way to its door
            poi.minCount = poi.maxCount = 1;
            poi.pieces.Clear();
            poi.pieces.Add( new PoiSO.Piece { prefab = house, offset = Vector2.zero, rotation = 0f } );
            EditorUtility.SetDirty( poi );
            return poi;
        }

        private static void AddToHome( PoiSO poi )
        {
            var home = AssetDatabase.LoadAssetAtPath<MapSO>( HomeMapPath );
            if( home == null || home.perlinNoiseConfig == null )
            {
                UnityEngine.Debug.LogError( $"[HomeHouseBuilder] {HomeMapPath} is missing or has no config" );
                return;
            }
            if( !home.perlinNoiseConfig.Pois.Contains( poi ) )
            {
                home.perlinNoiseConfig.Pois.Add( poi );
                EditorUtility.SetDirty( home.perlinNoiseConfig );
            }
        }
    }
}
