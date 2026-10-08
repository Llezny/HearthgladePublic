using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class GatherMotionTests {

        [ Test ]
        public void NotGathering_HasNoMotion( ) {
            Assert.AreEqual( GatherMotion.None, GatherMotionRules.For( false, GatherSkill.Harvest, ToolGroup.Sickle ) );
        }

        [ Test ]
        public void WhatNoToolHelps_IsPickedUp( ) {
            Assert.AreEqual( GatherMotion.Pickup, GatherMotionRules.For( true, GatherSkill.None, ToolGroup.None ) );
        }

        [ Test ]
        public void HarvestWithoutATool_IsDoneByHand( ) {
            Assert.AreEqual( GatherMotion.Hands, GatherMotionRules.For( true, GatherSkill.Harvest, ToolGroup.None ) );
        }

        [ Test ]
        public void HarvestWithASickle_UsesTheTool( ) {
            Assert.AreEqual( GatherMotion.Tool, GatherMotionRules.For( true, GatherSkill.Harvest, ToolGroup.Sickle ) );
        }

        [ Test ]
        public void RequiredTools_AreUsed( ) {
            Assert.AreEqual( GatherMotion.Tool, GatherMotionRules.For( true, GatherSkill.Chop, ToolGroup.Axe ) );
            Assert.AreEqual( GatherMotion.Tool, GatherMotionRules.For( true, GatherSkill.Mine, ToolGroup.Pickaxe ) );
        }
    }
}
