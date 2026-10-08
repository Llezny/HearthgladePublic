using System;

namespace Hearthglade.Core.Items
{
    // How long gathering takes: the base time divided by the speed of whatever the player works with.
    public static class GatherTime
    {
        // What bare hands manage; a tool slower than that is no use where hands are allowed.
        public const float BareHandSpeed = 1f;

        // The base time of work that names no resource.
        public const float DefaultSeconds = 6f;

        // The floor of the time, so a very fast tool never makes the loading bar vanish.
        public const float MinSeconds = 0.1f;

        // 0 = the work cannot be done (a tool is required and there is none).
        public static float EffectiveSpeed( ToolChoice tool, bool requiresTool ) {
            if( requiresTool ) {
                return tool.Found ? tool.Speed : 0f;
            }
            return tool.Found ? Math.Max( tool.Speed, BareHandSpeed ) : BareHandSpeed;
        }

        // Whether the tool actually does the work (and so wears): always when it is required, else only if it beats bare hands.
        public static bool UsesTool( ToolChoice tool, bool requiresTool ) {
            return tool.Found && ( requiresTool || tool.Speed > BareHandSpeed );
        }

        public static float Seconds( float baseSeconds, float speed ) {
            if( speed <= 0f ) {
                return float.PositiveInfinity;
            }
            return Math.Max( MinSeconds, baseSeconds / speed );
        }
    }
}
