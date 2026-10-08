using System;

namespace Hearthglade.Core.Items
{
    // What a piece of clothing shields the wearer from. Cold and Heat are points; Damage is the fraction of incoming damage taken off
    // (0.25 = a quarter), which is why the total is capped. Values add up over the worn pieces. Immutable.
    public readonly struct Protection
    {
        // Armour never makes the wearer immune.
        public const float MaxDamageReduction = 0.8f;

        public float Cold { get; }
        public float Heat { get; }
        public float Damage { get; }

        public Protection( float cold = 0f, float heat = 0f, float damage = 0f ) {
            Cold = Math.Max( 0f, cold );
            Heat = Math.Max( 0f, heat );
            Damage = Math.Max( 0f, damage );
        }

        // The share of damage that is actually taken off, never above MaxDamageReduction.
        public float DamageReduction => Math.Min( Damage, MaxDamageReduction );

        public bool IsEmpty => Cold <= 0f && Heat <= 0f && Damage <= 0f;

        // What is left of a blow after the armour: every source of damage to the player goes through this.
        public float Reduce( float rawDamage ) => Math.Max( 0f, rawDamage ) * ( 1f - DamageReduction );

        public static Protection operator +( Protection left, Protection right ) {
            return new Protection( left.Cold + right.Cold, left.Heat + right.Heat, left.Damage + right.Damage );
        }
    }
}
