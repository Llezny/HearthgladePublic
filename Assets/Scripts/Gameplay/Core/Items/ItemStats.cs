using System;

namespace Hearthglade.Core.Items
{
    // What an item does when it is held in the hand. Speeds are multipliers of gathering speed (bare hands = 1, see GatherTime);
    // 0 means the item is no good for that. Immutable.
    public readonly struct ItemStats
    {
        public float ChopSpeed { get; }
        public float HarvestSpeed { get; }
        public float MineSpeed { get; }
        public float AttackPower { get; }

        public ItemStats( float chopSpeed = 0f, float harvestSpeed = 0f, float mineSpeed = 0f, float attackPower = 0f ) {
            ChopSpeed = Math.Max( 0f, chopSpeed );
            HarvestSpeed = Math.Max( 0f, harvestSpeed );
            MineSpeed = Math.Max( 0f, mineSpeed );
            AttackPower = Math.Max( 0f, attackPower );
        }

        public float SpeedFor( GatherSkill skill ) {
            switch( skill ) {
                case GatherSkill.Chop: return ChopSpeed;
                case GatherSkill.Harvest: return HarvestSpeed;
                case GatherSkill.Mine: return MineSpeed;
                default: return 0f;
            }
        }

        public bool IsEmpty => ChopSpeed <= 0f && HarvestSpeed <= 0f && MineSpeed <= 0f && AttackPower <= 0f;
    }
}
