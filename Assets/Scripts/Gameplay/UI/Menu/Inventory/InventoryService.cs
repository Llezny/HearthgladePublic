using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json.Linq;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Inventory
{
    public sealed class InventoryService : ISaveable, IItemSlotActions {

        // TODO move it to some config file
        public const int INVENTORY_ROW_LENGTH = 5;
        public const int MIN_INVENTORY_ROWS = 10;

        public ItemContainer Container { get; private set; }
        public event Action<ItemSO, int> OnItemAdded;
        /// <summary>The player chose to put a buildable item of the backpack down; whoever builds takes it from there (the item stays in the backpack until it is put down).</summary>
        public event Action<ItemSO> OnPlaceRequested;
        // Raised when items did not fit: what could not be added, or what the player was not allowed to pick up.
        public event Action<ItemSO, int> OnItemRejected;
        public event Action<ItemDefinition, NutritionOverride> OnItemUsed;
        public event Action<ItemSlot, ItemSlot> OnItemMoved;
        private readonly SaveManager saveManager;
        private readonly ItemCatalog catalog;

        [ Inject ]
        public InventoryService( SaveManager saveManager, ItemCatalog catalog ) {
            this.saveManager = saveManager;
            this.catalog = catalog;
            saveManager.RegisterISavable(this);
            Container = new ItemContainer( INVENTORY_ROW_LENGTH * MIN_INVENTORY_ROWS );
            if( saveManager.TryGetState<InventoryService>( out var gameState) ) {
                RestoreState(gameState);
            }
        }

        /// <summary>The backpack has at least this many rows (it only grows, see the ship tree).</summary>
        public void EnsureRows( int rows ) {
            Container.Grow( INVENTORY_ROW_LENGTH * rows );
        }

        // Only the filled slots, keyed by slot index.
        public object CaptureState(){
            var inventoryItems = new Dictionary<int, ItemStackData>();
            for( int i = 0; i < Container.Size; i++ ) {
                var data = ItemStackData.From( Container[ i ].Stack );
                if( data != null ) {
                    inventoryItems.Add( i, data );
                }
            }
            return inventoryItems;
        }

        public void RestoreState( object state ) {
            var inventoryItems = JToken.Parse( state.ToString( ) ).ToObject<Dictionary<int, ItemStackData>>();
            // A backpack that grew with the ship tree: the rows come back before the items that lay in them.
            int highest = -1;
            foreach( var key in inventoryItems.Keys ) {
                highest = Math.Max( highest, key );
            }
            EnsureRows( ( highest + INVENTORY_ROW_LENGTH ) / INVENTORY_ROW_LENGTH );
            foreach( var pair in inventoryItems ) {
                if( pair.Key < 0 || pair.Key >= Container.Size || pair.Value == null || !pair.Value.TryToStack( catalog, out var stack ) ) {
                    continue;
                }
                var slot = Container[ pair.Key ];
                if( slot.Accepts( stack.Definition ) ) {
                    slot.Set( stack );
                }
            }
        }

        // Adds as much as fits; whatever does not fit is reported through OnItemRejected and returned as the remainder.
        public AddResult AddItem(ItemSO itemSo, int quantity = 1) {
            if( itemSo == null || !catalog.TryGet( itemSo.Id, out var definition ) ) {
                UnityEngine.Debug.LogError( "Can't add null item, skipping" );
                return new AddResult( 0, Math.Max( quantity, 0 ) );
            }
            var result = Container.Add( definition, quantity );
            if( result.Added > 0 ) {
                OnItemAdded?.Invoke( itemSo, result.Added );
            }
            if( !result.IsComplete ) {
                OnItemRejected?.Invoke( itemSo, result.Remainder );
            }
            return result;
        }

        public bool CanFit( ItemSO itemSo, int quantity = 1 ) {
            return itemSo != null && catalog.TryGet( itemSo.Id, out var definition ) && Container.CanFit( definition, quantity );
        }

        // For code that refused to start something because the items would not fit.
        public void ReportNoRoom( ItemSO itemSo, int quantity ) {
            OnItemRejected?.Invoke( itemSo, quantity );
        }

        public void UseItem( ItemSlot slot ) {
            if( slot == null || slot.IsEmpty ) {
                return;
            }
            var stack = slot.Stack;
            if( !stack.Definition.IsUsable ) {
                return;
            }
            // Read before Remove, which clears the slot (and its nutrition override) once emptied.
            var nutrition = stack.EffectiveNutrition;
            slot.Remove( 1 );
            OnItemUsed?.Invoke( stack.Definition, nutrition );
        }

        public void PlaceItem( ItemSlot slot ) {
            if( slot == null || slot.IsEmpty || ( SlotActions.Available( slot ) & SlotAction.Place ) == 0 ) {
                return;
            }
            var item = catalog.GetAsset( slot.Stack.Definition );
            if( item != null ) {
                OnPlaceRequested?.Invoke( item );
            }
        }

        public int Count( ItemId id ) => Container.Count( id );

        public void RemoveItem( ItemId id, int itemsToRemoveNum = 1 ) {
            if( !Container.TryRemove( id, itemsToRemoveNum ) ) {
                UnityEngine.Debug.Log( "You dont have enough " + id );
            }
        }

        public bool HasItems( IEnumerable<Requirement> cost ) => Container.HasAll( ToAmounts( cost ) );

        // All or nothing: nothing is taken when any of the items is missing.
        public bool RemoveItems( IEnumerable<Requirement> cost ) {
            if( Container.TryRemove( ToAmounts( cost ) ) ) {
                return true;
            }
            UnityEngine.Debug.Log( "You dont have enough items to pay that" );
            return false;
        }

        /// <summary>The hunger points of all the food in the backpack.</summary>
        public float FoodPointsAvailable => FoodBudget.Available( Container );

        public bool CanPay( float foodPoints, IEnumerable<Requirement> items ) {
            return HasItems( items ) && FoodBudget.TryPlan( Container, foodPoints, out _ );
        }

        // All or nothing: the items and the food are checked first, nothing is taken when either is short.
        public bool TryPay( float foodPoints, IEnumerable<Requirement> items ) {
            if( !HasItems( items ) || !FoodBudget.TryPlan( Container, foodPoints, out var plan ) ) {
                UnityEngine.Debug.Log( "You dont have enough supplies to pay that" );
                return false;
            }
            Container.TryRemove( ToAmounts( items ) );
            FoodBudget.Apply( Container, plan );
            return true;
        }

        private static IEnumerable<ItemAmount> ToAmounts( IEnumerable<Requirement> cost ) {
            foreach( var requirement in cost ) {
                yield return new ItemAmount( requirement.Item, requirement.requiredQuantity );
            }
        }

        public bool HasItem( ItemSO itemSO ) {
            return Container.Contains( itemSO.Id );
        }

        public bool TryMoveItem( ItemSlot from, ItemSlot to ) {
            if( ItemTransfer.Move( from, to ) == MoveResult.Rejected ) {
                return false;
            }
            OnItemMoved?.Invoke( from, to );
            return true;
        }
    }
}
