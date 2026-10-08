using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Player;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;

namespace Hearthglade.Gameplay.Entities
{
    /// <summary>
    /// The player's side of a hunt (docs/EQUIPMENT_PLAN.md, phase 7): which weapon they strike with, how hard, the weapon wearing down, and the
    /// meat going into the backpack. Animals come from a pool without injection, so they reach it through their <see cref="Map.Map"/>.
    /// One strike is one interaction: the player thrusts once, the animal is hit at the end of it and runs; the next tap chases it.
    /// </summary>
    public sealed class HuntingService
    {
        public const string NoWeaponMessage = "I need a spear to hunt.";

        private readonly PlayerToolService tools;
        private readonly InventoryService inventory;
        private readonly PlayerController player;

        public HuntingService( PlayerToolService tools, InventoryService inventory, PlayerController player ) {
            this.tools = tools;
            this.inventory = inventory;
            this.player = player;
        }

        public bool HasWeapon => tools.PickWeapon().Found;

        // Where the blow comes from: the struck animal runs away from here.
        public Vector3 PlayerPosition => player.transform.position;

        // The thrust starts: the weapon shows in the hand (and picks the animation), the player turns to the prey.
        public void BeginStrike( Transform prey ) {
            tools.Begin( tools.PickWeapon() );
            if( prey != null ) {
                var towards = prey.position - player.transform.position;
                towards.y = 0f;
                if( towards.sqrMagnitude > 1e-6f ) {
                    player.transform.rotation = Quaternion.LookRotation( towards );
                }
            }
        }

        // The thrust lands: how hard it hit. The weapon wears down by one use.
        public float LandStrike() {
            float power = tools.InUse.Found ? tools.InUse.Speed : 0f;
            tools.Complete();
            return power;
        }

        public void CancelStrike() {
            tools.Cancel();
        }

        // The catch goes into the backpack; what does not fit is lost, and the player is told.
        public void TakeCatch( ItemSO meat, int count ) {
            if( meat == null || count <= 0 ) {
                return;
            }
            if( !inventory.CanFit( meat, count ) ) {
                inventory.ReportNoRoom( meat, count );
                return;
            }
            inventory.AddItem( meat, count );
        }
    }
}
