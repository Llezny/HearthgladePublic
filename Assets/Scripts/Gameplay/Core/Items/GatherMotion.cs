namespace Hearthglade.Core.Items
{
    // How the player's body moves while gathering something. The animation picker turns it into a clip.
    public enum GatherMotion
    {
        // Not gathering at all (a door, a trader...): no work animation.
        None = 0,
        // Picked up by hand, no tool ever helps: sticks, mushrooms, herbs.
        Pickup = 1,
        // Gathered by hand because no tool is in use, although one could help: crops, fruit, bushes.
        Hands = 2,
        // Worked with the tool in use, in the motion of its group: an axe on wood, a pickaxe on stone, a sickle on wheat.
        Tool = 3,
    }

    public static class GatherMotionRules
    {
        // The three cases of gathering: a tool in use always shows (it is only in use when it helps, see GatherTime.UsesTool);
        // without one, what no tool can help with is picked up and the rest is gathered by hand.
        public static GatherMotion For( bool isGathering, GatherSkill skill, ToolGroup toolInUse ) {
            if( !isGathering ) {
                return GatherMotion.None;
            }
            if( toolInUse != ToolGroup.None ) {
                return GatherMotion.Tool;
            }
            return skill == GatherSkill.None ? GatherMotion.Pickup : GatherMotion.Hands;
        }
    }
}
