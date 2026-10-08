using UnityEngine;

namespace Hearthglade.EditorTools
{
    // The socket in the right hand of a character of the kit that carries the tool in hand: the tool prefabs have the origin at the grip, the shaft
    // along +Y and the edge or point along +Z (stone_tools.py), so this pose alone decides how every tool sits. Shared by the preview and by
    // the player, so what is judged in the preview is what the game shows.
    public static class ToolSocket
    {
        public const string Name = "ToolSocket";

        // In the frame of the mixamorig:RightHand bone; worked out by FitToGrip.
        public static Vector3 LocalPosition = new Vector3( 0f, 0.044f, -0.012f );
        public static Vector3 LocalEuler = new Vector3( 270f, 270f, 0f );

        // For trying a pose without recompiling: TOOL_SOCKET_POS and TOOL_SOCKET_EULER as "x,y,z".
        public static void OverrideFromEnvironment()
        {
            if( TryParse( "TOOL_SOCKET_POS", out var position ) )
            {
                LocalPosition = position;
            }
            if( TryParse( "TOOL_SOCKET_EULER", out var euler ) )
            {
                LocalEuler = euler;
            }
        }

        private static bool TryParse( string variable, out Vector3 value )
        {
            value = default;
            var text = System.Environment.GetEnvironmentVariable( variable );
            if( string.IsNullOrEmpty( text ) )
            {
                return false;
            }
            var parts = text.Split( ',' );
            value = new Vector3(
                float.Parse( parts[ 0 ], System.Globalization.CultureInfo.InvariantCulture ),
                float.Parse( parts[ 1 ], System.Globalization.CultureInfo.InvariantCulture ),
                float.Parse( parts[ 2 ], System.Globalization.CultureInfo.InvariantCulture ) );
            return true;
        }

        // The grip of tool_animations.py: the shaft crosses the palm at this share of the hand bone (0.08 m long) and this far under it.
        private const float HandLength = 0.08f;
        private const float GripAlong = 0.55f;
        private const float PalmDepth = 0.012f;
        private const string PlayerPrefabPath = "Assets/_Prefabs/Player/Player.prefab";

        // Works the socket out from the kit's model in its T-pose (where the right hand points along +X, palm down, thumb forward), with the grip
        // of the Blender clips: the edge the way the fingers point, the shaft the way the thumb points. Prints the values for LocalPosition and
        // LocalEuler and puts the socket of the player there.
        [ UnityEditor.MenuItem( "Tools/Agent Tools/Characters/Fit tool socket to the grip of the clips" ) ]
        public static void FitToGrip()
        {
            var model = Object.Instantiate( UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>( CharacterAssetBuilder.ModelPath ) );
            try
            {
                model.transform.SetPositionAndRotation( Vector3.zero, Quaternion.identity );
                model.transform.localScale = Vector3.one;
                var hand = ToolPreviewRunner.FindBone( model.transform, "mixamorig:RightHand" );
                var fingers = ( hand.position - hand.parent.position ).normalized;
                var position = hand.position + fingers * ( GripAlong * HandLength ) + Vector3.down * PalmDepth;
                var rotation = Quaternion.LookRotation( fingers, Vector3.forward );
                LocalPosition = hand.InverseTransformPoint( position );
                LocalEuler = ( Quaternion.Inverse( hand.rotation ) * rotation ).eulerAngles;
                UnityEngine.Debug.Log( $"[ToolSocket] fingers {fingers:F3}; LocalPosition = {LocalPosition.x:F5}, {LocalPosition.y:F5}, {LocalPosition.z:F5}; " +
                                       $"LocalEuler = {LocalEuler.x:F3}, {LocalEuler.y:F3}, {LocalEuler.z:F3}" );
            }
            finally
            {
                Object.DestroyImmediate( model );
            }

            var root = UnityEditor.PrefabUtility.LoadPrefabContents( PlayerPrefabPath );
            try
            {
                var hand = ToolPreviewRunner.FindBone( root.transform, "mixamorig:RightHand" );
                Attach( hand );
                UnityEditor.PrefabUtility.SaveAsPrefabAsset( root, PlayerPrefabPath );
            }
            finally
            {
                UnityEditor.PrefabUtility.UnloadPrefabContents( root );
            }
        }

        public static Transform Attach( Transform hand )
        {
            var existing = hand.Find( Name );
            var socket = existing != null ? existing : new GameObject( Name ).transform;
            socket.SetParent( hand, false );
            socket.localPosition = LocalPosition;
            socket.localRotation = Quaternion.Euler( LocalEuler );
            socket.localScale = Vector3.one;
            return socket;
        }
    }
}
