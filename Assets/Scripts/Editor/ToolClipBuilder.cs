using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hearthglade.EditorTools
{
    // Imports the work clips of the hand tools (docs/EQUIPMENT_PLAN.md, phase 3). They are animated in Blender (Tools/Agent Tools/Blender/
    // tool_animations.py: the hands put on the shaft by IK, the feet planted) and exported as the kit's armature alone, one FBX per clip; here
    // they become Humanoid clips on the kit's avatar, so the player (and every character of the kit) can play them. Each loop strikes at the
    // middle of its cycle (Strike), which is where the game puts the hit. Importing again keeps the guids.
    public static class ToolClipBuilder
    {
        public const string ClipFolder = "Assets/Arts/Animation/Tools";
        public const string AxeChopPath = ClipFolder + "/Tool@AxeChop.fbx";
        public const string PickaxeStrikePath = ClipFolder + "/Tool@PickaxeStrike.fbx";
        public const string SickleReapPath = ClipFolder + "/Tool@SickleReap.fbx";
        public const string SpearThrustPath = ClipFolder + "/Tool@SpearThrust.fbx";
        public const string PickupPath = ClipFolder + "/Hands@Pickup.fbx";
        public const string HandsHarvestPath = ClipFolder + "/Hands@Harvest.fbx";

        // The strike lies at this share of the cycle (STRIKE in tool_animations.py; the hit event of the player animation uses it).
        public const float Strike = 0.5f;

        private static readonly string[] Paths = { AxeChopPath, PickaxeStrikePath, SickleReapPath, SpearThrustPath, PickupPath, HandsHarvestPath };

        // The muscle clips this importer replaces.
        private static readonly string[] Retired =
        {
            ClipFolder + "/Tool_AxeChop.anim", ClipFolder + "/Tool_SickleReap.anim", ClipFolder + "/Tool_SpearThrust.anim",
        };

        [ MenuItem( "Tools/Agent Tools/Characters/Import tool clips" ) ]
        public static void BuildAll()
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath( CharacterAssetBuilder.ModelPath ).OfType<Avatar>().FirstOrDefault();
            if( avatar == null || !avatar.isHuman )
            {
                throw new System.InvalidOperationException( $"{CharacterAssetBuilder.ModelPath} has no Humanoid avatar" );
            }
            foreach( var path in Paths )
            {
                Configure( path, avatar );
            }
            foreach( var path in Retired )
            {
                AssetDatabase.DeleteAsset( path );
            }
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log( $"[ToolClipBuilder] {Paths.Length} tool clips are imported" );
        }

        public static AnimationClip Load( string path )
        {
            return AssetDatabase.LoadAllAssetsAtPath( path ).OfType<AnimationClip>().FirstOrDefault( c => !c.name.StartsWith( "__preview__" ) );
        }

        private static void Configure( string path, Avatar avatar )
        {
            AssetDatabase.ImportAsset( path, ImportAssetOptions.ForceSynchronousImport );
            var importer = ( ModelImporter ) AssetImporter.GetAtPath( path );
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            // The file holds the armature alone, which Unity would fold into the root: the avatar wants Armature/mixamorig:Hips.
            importer.preserveHierarchy = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();

            importer = ( ModelImporter ) AssetImporter.GetAtPath( path );
            var clip = importer.defaultClipAnimations.First();
            clip.name = Path.GetFileNameWithoutExtension( path ).Split( '@' )[ 1 ];
            clip.loopTime = true;
            clip.loopPose = false; // the last frame is the first one already
            // The character stays where it stands and faces where it faces; the hips sinking and turning stay in the pose.
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }
    }
}
