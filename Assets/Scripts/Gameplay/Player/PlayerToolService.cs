using System;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Resource;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using VContainer;

namespace Hearthglade.Gameplay.Player
{
    /// <summary>
    /// The player's tools for gathering. It picks the best tool the player carries (the hand or the backpack, see <see cref="ToolSelector"/>)
    /// without touching the hand slot, tells how long the work takes, and wears the tool out when the work is done.
    /// Asking is free of side effects; <see cref="Begin"/>, <see cref="Complete"/> and <see cref="Cancel"/> frame one interaction.
    /// </summary>
    public sealed class PlayerToolService
    {
        private readonly InventoryService inventory;
        private readonly EquipmentService equipment;
        private readonly ItemCatalog catalog;
        private readonly MurmurService murmur;

        /// <summary>The tool used by the interaction in progress; <see cref="ToolChoice.Found"/> is false for bare hands or no interaction.</summary>
        public ToolChoice InUse { get; private set; }

        /// <summary>Raised when an interaction starts with a tool, and when it ends (finished or cancelled).</summary>
        public event Action<ToolChoice> UseStarted;
        public event Action UseEnded;

        [ Inject ]
        public PlayerToolService( InventoryService inventory, EquipmentService equipment, ItemCatalog catalog, MurmurService murmur ) {
            this.inventory = inventory;
            this.equipment = equipment;
            this.catalog = catalog;
            this.murmur = murmur;
        }

        public ToolChoice Pick( GatherSkill skill ) {
            return ToolSelector.Pick( skill, equipment.Model[ SlotType.Hand ], inventory.Container );
        }

        /// <summary>Whether the player has what the resource needs (always true when bare hands will do).</summary>
        public bool CanGather( ResourceSO resource ) {
            return resource == null || GatherTime.EffectiveSpeed( Pick( resource.Skill ), RequiresTool( resource ) ) > 0f;
        }

        public float GatherSeconds( ResourceSO resource ) {
            if( resource == null ) {
                return GatherTime.Seconds( GatherTime.DefaultSeconds, GatherTime.BareHandSpeed );
            }
            return GatherTime.Seconds( resource.BaseSeconds, GatherTime.EffectiveSpeed( Pick( resource.Skill ), RequiresTool( resource ) ) );
        }

        /// <summary>What the player murmurs when the resource needs a tool they lack; null when nothing is missing.</summary>
        public string MissingToolMessage( ResourceSO resource ) {
            if( resource == null || CanGather( resource ) ) {
                return null;
            }
            switch( resource.Skill ) {
                case GatherSkill.Chop: return "I need an axe to cut this down.";
                case GatherSkill.Mine: return "I need a pickaxe to break this.";
                default: return "I need a proper tool for this.";
            }
        }

        /// <summary>The weapon the player would hunt with (see <see cref="ToolSelector.PickWeapon"/>); its Speed is its AttackPower.</summary>
        public ToolChoice PickWeapon() {
            return ToolSelector.PickWeapon( equipment.Model[ SlotType.Hand ], inventory.Container );
        }

        public void Begin( ResourceSO resource ) {
            End();
            if( resource == null ) {
                return;
            }
            var tool = Pick( resource.Skill );
            if( GatherTime.UsesTool( tool, RequiresTool( resource ) ) ) {
                Begin( tool );
            }
        }

        /// <summary>Starts work with a tool chosen elsewhere (a weapon for a hunt): it shows in the hand and wears on <see cref="Complete"/>.</summary>
        public void Begin( ToolChoice tool ) {
            End();
            if( !tool.Found ) {
                return;
            }
            InUse = tool;
            UseStarted?.Invoke( tool );
        }

        /// <summary>The work is done: the tool that did it wears down, and is gone when its durability runs out.</summary>
        public void Complete() {
            var tool = InUse;
            End();
            if( !tool.Found || tool.Slot == null || tool.Slot.IsEmpty || tool.Slot.Stack.Id != tool.Definition.Id ) {
                return;
            }
            if( ToolWear.Apply( tool.Slot ) == WearResult.Broken ) {
                var asset = catalog.GetAsset( tool.Definition );
                murmur.Show( $"My {( asset != null ? asset.itemName : tool.Definition.Id.Value )} broke." );
            }
        }

        public void Cancel() {
            End();
        }

        private void End() {
            if( InUse.Found ) {
                InUse = ToolChoice.None;
                UseEnded?.Invoke();
            }
        }

        private static bool RequiresTool( ResourceSO resource ) {
            return resource.RequiresTool && resource.Skill != GatherSkill.None;
        }
    }
}
