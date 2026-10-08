using System;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Helpers.Extensions;
using Hearthglade.Gameplay.Items.UsableItems;
using Hearthglade.Gameplay.UI.Menu.Cooking;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment.Cooking {

    [ Serializable ]
    public class CookingStationState {

        public Action<float> FuelChanged;
        public Action<float> CookingProgressChanged;
        public Action<CookingStationLevel> LevelChanged;

        public float FuelLeft;
        public float CookingProgress;
        public float CookTimeSeconds = 10f;
        public ItemId TargetItem;
        public NutritionOverride PendingNutrition;
        public int PendingPortions;
        public CookingStationLevel Level { get; private set; }

        // Each station keeps its own staging slots, sized for the highest tier; slots beyond the
        // current level's IngredientSlotCount() are locked (see SetLevel).
        public readonly ItemContainer Ingredients;

        // Where a finished dish waits after cooking completes, until the player drags it out.
        public readonly ItemSlot OutputSlot = new ItemSlot { OutputOnly = true };

        // Drop a fuel item here to burn it; CookingService consumes it automatically (see
        // TryConsumeFuel) instead of a separate "Add Fuel" button. Only burnable items are accepted.
        public readonly ItemSlot FuelSlot = new ItemSlot();

        public CookingStationState( CookingStationLevel initialLevel = CookingStationLevel.Fireplace ) {
            FuelSlot.Filter = item => item.IsFuel;
            Ingredients = new ItemContainer( CookingStationLevelExtensions.MaxIngredientSlots );
            SetLevel( initialLevel );
        }

        public void SetLevel( CookingStationLevel level ) {
            Level = level;
            var usableSlots = level.IngredientSlotCount();
            for( int i = 0; i < Ingredients.Size; i++ ) {
                Ingredients[ i ].IsLocked = i >= usableSlots;
            }
            LevelChanged?.Invoke( level );
        }

        // Only touches slots usable at the current tier - tier-locked slots stay locked either way.
        public void SetIngredientSlotsLockedForCooking( bool locked ) {
            var usableSlots = Level.IngredientSlotCount();
            for( int i = 0; i < usableSlots; i++ ) {
                Ingredients[ i ].IsLocked = locked;
            }
        }

        // The fire is lit while there is fuel left: it shows flames, cooks and warms whoever stands close.
        public bool IsBurning => FuelLeft > CookingService.MinFuel;

        private bool CanProgress( float value ) {
            return IsBurning && !TargetItem.IsEmpty && CookingProgress < CookingService.MaxProgress;
        }

        public void AddFuel( float value ) {
            FuelLeft = Math.Clamp( FuelLeft + value, 0, CookingService.MaxFuel );
            FuelChanged?.Invoke( FuelLeft );
        }

        // Clamped rather than rejected on overshoot - otherwise a delta-time increment that would
        // land past MaxProgress got blocked outright, leaving progress stuck just under it forever
        // and cooking never completing.
        public void Progress( float value ) {
            CookingProgress = Math.Min( CookingProgress + value, CookingService.MaxProgress );
            CookingProgressChanged?.Invoke( CookingProgress );
        }

        public void ResetProgress() {
            CookingProgress = 0;
            CookingProgressChanged?.Invoke( CookingProgress );
        }

        public void Update( float deltaTime ) {
            var fuelFactor = -deltaTime / CookingService.SecondsPerFuel;
            AddFuel( fuelFactor );

            if( TargetItem.IsEmpty ) {
                return;
            }
            var progressFactor = deltaTime / Mathf.Max( CookTimeSeconds, 0.01f ) * CookingService.MaxProgress;
            if( CanProgress( progressFactor ) ) {
                Progress( progressFactor );
            }
        }
    }
}
