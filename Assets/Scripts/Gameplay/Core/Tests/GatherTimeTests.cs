using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class GatherTimeTests {

        private static ToolChoice ToolOfSpeed( float speed ) {
            var item = TestItems.Tool( "Tool", new ItemStats( chopSpeed: speed ) );
            return new ToolChoice( new ItemSlot(), item, speed );
        }

        [ Test ]
        public void Seconds_AreTheBaseTimeDividedBySpeed( ) {
            Assert.AreEqual( 6f, GatherTime.Seconds( 6f, 1f ), 1e-4f );
            Assert.AreEqual( 3f, GatherTime.Seconds( 6f, 2f ), 1e-4f );
            Assert.AreEqual( 12f, GatherTime.Seconds( 6f, 0.5f ), 1e-4f );
        }

        [ Test ]
        public void Seconds_NeverDropBelowTheFloor( ) {
            Assert.AreEqual( GatherTime.MinSeconds, GatherTime.Seconds( 6f, 1000f ), 1e-4f );
        }

        [ Test ]
        public void NoSpeed_MeansTheWorkCannotBeDone( ) {
            Assert.IsTrue( float.IsPositiveInfinity( GatherTime.Seconds( 6f, 0f ) ) );
        }

        [ Test ]
        public void RequiredToolMissing_GivesNoSpeed( ) {
            Assert.AreEqual( 0f, GatherTime.EffectiveSpeed( ToolChoice.None, requiresTool: true ) );
        }

        [ Test ]
        public void RequiredToolPresent_GivesItsSpeed( ) {
            Assert.AreEqual( 2f, GatherTime.EffectiveSpeed( ToolOfSpeed( 2f ), requiresTool: true ) );
            Assert.AreEqual( 0.5f, GatherTime.EffectiveSpeed( ToolOfSpeed( 0.5f ), requiresTool: true ) );
        }

        [ Test ]
        public void OptionalTool_BareHandsDoTheWork( ) {
            Assert.AreEqual( GatherTime.BareHandSpeed, GatherTime.EffectiveSpeed( ToolChoice.None, requiresTool: false ) );
        }

        [ Test ]
        public void OptionalTool_SpeedsUpTheWork( ) {
            Assert.AreEqual( 2.5f, GatherTime.EffectiveSpeed( ToolOfSpeed( 2.5f ), requiresTool: false ) );
        }

        [ Test ]
        public void OptionalTool_SlowerThanHandsIsNotWorse( ) {
            Assert.AreEqual( GatherTime.BareHandSpeed, GatherTime.EffectiveSpeed( ToolOfSpeed( 0.4f ), requiresTool: false ) );
        }
    }
}
