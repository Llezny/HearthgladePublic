using System;
using System.Collections.Generic;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment.Cooking;
using Hearthglade.Gameplay.Helpers.Extensions;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Items.UsableItems;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Newtonsoft.Json;
using UnityEngine;
using VContainer.Unity;

namespace Hearthglade.Gameplay.UI.Menu.Cooking {
    public class CookingService : ITickable {
        //Todo would be nice to make some service to tag items instead of matching by name, same as fuel below
        public const int MaxFuel = 10, MinFuel = 0;
        public const int MaxProgress = 10, MinProgress = 0;
        // How long one point of fuel burns: a log of wood (FuelValue 1) keeps the fire going for a minute.
        public const float SecondsPerFuel = 60f;

        public Action<CookingStation, CookingStationState> ActiveCookingStationChanged;
        public Action<CookingStation, CookingStationState> StationUpgraded;
        public Action<CookingRecipeSO> CookingStarted;
        // Fired on ingredient changes so the UI can preview what's about to be cooked; "" clears it.
        public Action<CookingStation, string> RecipePreviewChanged;
        private const string NoRecipeMatchMessage = "Seriously, you'd eat that?";
        private Dictionary<CookingStation, CookingStationState> registeredCookingStations = new();
        private CookingStation currentCookingStation;
        private CookingStationState currentCookingStationState => registeredCookingStations[currentCookingStation];

        // Dependencies
        private readonly SaveManager saveManager;
        private readonly References references;
        private readonly ItemCatalog catalog;
        //

        public CookingService( SaveManager saveManager, References references, ItemCatalog catalog ) {
            this.catalog = catalog;
            this.saveManager = saveManager;
            this.references = references;
        }

        // A station with fuel left is burning (its fuel burns down whether it cooks or not), and a burning one warms whoever stands close.
        public bool IsFireBurningNear( Vector3 position, float radius ) {
            foreach( var kvp in registeredCookingStations ) {
                if( kvp.Key != null && kvp.Value.IsBurning
                    && ( kvp.Key.transform.position - position ).sqrMagnitude <= radius * radius ) {
                    return true;
                }
            }
            return false;
        }

        public CookingStationState StateOf( CookingStation station ) {
            return registeredCookingStations.TryGetValue( station, out var state ) ? state : null;
        }

        public void Tick() {
            foreach( var kvp in registeredCookingStations ) {
                var station = kvp.Key;
                var state = kvp.Value;
                state.Update( Time.deltaTime );
                TryConsumeFuel( state, state.FuelSlot );
                if( state.CookingProgress >= MaxProgress && !state.TargetItem.IsEmpty ) {
                    CompleteCooking( station, state );
                }
            }
        }

        // Finished dishes wait in the station's own output slot instead of jumping straight into the
        // player's inventory, so the player has to come back and collect them. If the output slot is
        // still occupied by an earlier, uncollected batch this keeps retrying next Tick instead of
        // silently discarding the new dish.
        private void CompleteCooking( CookingStation station, CookingStationState state ) {
            catalog.TryGet( state.TargetItem, out var dish );
            var portions = Math.Max( state.PendingPortions, 1 );
            if( dish != null && state.OutputSlot.SpaceFor( dish ) < portions ) {
                return;
            }
            if( dish != null ) {
                state.OutputSlot.Add( ItemStack.Of( dish, portions ).WithNutrition( state.PendingNutrition ) );
            }
            state.TargetItem = default;
            state.ResetProgress();
            state.PendingNutrition = default;
            state.PendingPortions = 0;
            state.SetIngredientSlotsLockedForCooking( false );
            UpdateRecipePreview( station, state );
        }

        // Only one fuel item burns at a time; the rest wait in the slot until FuelLeft hits 0.
        private void TryConsumeFuel( CookingStationState state, ItemSlot slot ) {
            if( state.FuelLeft > MinFuel || slot.IsEmpty ) {
                return;
            }
            var item = slot.Stack.Definition;
            if( !item.IsFuel ) {
                return;
            }
            state.AddFuel( item.FuelValue );
            slot.Remove( 1 );
        }

        public void SetActiveCookingStation( CookingStation newActiveCookingStation ) {
            this.currentCookingStation = newActiveCookingStation;
            ActiveCookingStationChanged?.Invoke( newActiveCookingStation, registeredCookingStations[newActiveCookingStation] );
        }

        public CookingStationLevel GetCookingStationLevel( CookingStation station ) {
            return registeredCookingStations.TryGetValue( station, out var state ) ? state.Level : CookingStationLevel.Fireplace;
        }

        private static bool IsPot( ItemSlot slot ) => !slot.IsEmpty && slot.Stack.Definition.HasTag( ItemTag.CookingPot );

        private ItemDefinition FindPot() {
            foreach( var item in catalog.All ) {
                if( item.HasTag( ItemTag.CookingPot ) ) {
                    return item;
                }
            }
            return null;
        }

        private string DisplayName( ItemId id ) {
            var asset = catalog.GetAsset( id );
            return asset != null ? asset.itemName : id.ToString();
        }

        // Slot 0 doubles as the Pot's own holder: dropping a Pot there upgrades the station in place
        // (no separate "upgrade" button) and the Pot stays put instead of being consumed, unlocking 3
        // more ingredient slots. Dragging the Pot back out downgrades the station again. Slot 0 is
        // always unlocked at Fireplace level and always exists, so it's the only one that needs watching.
        private void OnPotSlotChanged( CookingStation station, CookingStationState state, ItemSlot slot ) {
            if( state.Level == CookingStationLevel.Fireplace && IsPot( slot ) ) {
                state.SetLevel( CookingStationLevel.Pot );
                StationUpgraded?.Invoke( station, state );
            }
            else if( state.Level == CookingStationLevel.Pot && !IsPot( slot ) ) {
                state.SetLevel( CookingStationLevel.Fireplace );
                StationUpgraded?.Invoke( station, state );
            }
        }

        public void StartCooking(  ) {
            if( currentCookingStationState.FuelLeft < 0.05f ) {
                return;
            }
            var recipe = GetMatchingRecipe( currentCookingStationState );
            if( recipe is null ) {
                return;
            }
            var portions = ComputePortionCount( recipe, currentCookingStationState.Ingredients );
            if( portions <= 0 ) {
                return;
            }
            currentCookingStationState.PendingNutrition = ComputeNutrition( recipe, currentCookingStationState.Ingredients );
            currentCookingStationState.PendingPortions = portions;
            currentCookingStationState.TargetItem = recipe.TargetItem.Id;
            currentCookingStationState.CookTimeSeconds = recipe.BaseTimeToCookInSeconds * portions;
            currentCookingStationState.CookingProgress = 0;
            currentCookingStationState.SetIngredientSlotsLockedForCooking( true );
            ConsumeIngredients( recipe, portions );

            CookingStarted?.Invoke(recipe);
        }

        // How many whole portions the staged ingredients can cover - the smallest ratio across all
        // required categories, so leftovers that don't complete another portion stay in their slot.
        private int ComputePortionCount( CookingRecipeSO recipe, ItemContainer ingredients ) {
            if( recipe.Ingredients.Count == 0 ) {
                return 0;
            }
            int portions = int.MaxValue;
            foreach( var required in recipe.Ingredients ) {
                if( required.Quantity <= 0 ) {
                    continue;
                }
                var available = ingredients.CountFood( required.Item );
                portions = Math.Min( portions, available / required.Quantity );
            }
            return Math.Max( portions, 0 );
        }

        private NutritionOverride ComputeNutrition( CookingRecipeSO recipe, ItemContainer ingredients ) {
            float totalQuality = 0f;
            int totalWeight = 0;
            foreach( var required in recipe.Ingredients ) {
                totalQuality += GetAverageQualityForFoodType( ingredients, required.Item ) * required.Quantity;
                totalWeight += required.Quantity;
            }
            var qualityMultiplier = totalWeight > 0 ? totalQuality / totalWeight : 1f;
            if( recipe.TargetItem is not IUsableItem dish ) {
                return default;
            }
            return new NutritionOverride(
                dish.HungerHealing * qualityMultiplier,
                dish.ThirstHealing * qualityMultiplier,
                dish.HealthHealing * qualityMultiplier
            );
        }

        private float GetAverageQualityForFoodType( ItemContainer ingredients, FoodType foodType ) {
            float totalQuality = 0f;
            int totalCount = 0;
            foreach( var slot in ingredients ) {
                if( slot.IsEmpty || ( slot.Stack.Definition.FoodType & foodType ) == 0 ) {
                    continue;
                }
                totalQuality += slot.Stack.Definition.NutritionQuality * slot.Stack.Count;
                totalCount += slot.Stack.Count;
            }
            return totalCount > 0 ? totalQuality / totalCount : 1f;
        }

        // Only consumes exactly what this batch needs per category, leaving any leftover (not enough
        // for another whole portion) sitting in its slot instead of clearing everything.
        private void ConsumeIngredients( CookingRecipeSO recipe, int portions ) {
            foreach( var required in recipe.Ingredients ) {
                ConsumeIngredientCategory( required.Item, required.Quantity * portions );
            }
        }

        private void ConsumeIngredientCategory( FoodType foodType, int amountNeeded ) {
            foreach( var slot in currentCookingStationState.Ingredients ) {
                if( amountNeeded <= 0 ) {
                    return;
                }
                if( slot.IsEmpty || ( slot.Stack.Definition.FoodType & foodType ) == 0 ) {
                    continue;
                }
                amountNeeded -= slot.Remove( amountNeeded );
            }
        }

        private CookingRecipeSO GetMatchingRecipe( CookingStationState state ) {
            foreach( var recipe in references.CookingRecipeDatabase ) {
                if( recipe.MinCookingStationLevel <= state.Level && IngredientsMatchesRecipe( recipe, state ) ) {
                    return recipe;
                }
            }
            return null;
        }

        private bool IngredientsMatchesRecipe( CookingRecipeSO recipe, CookingStationState state ) {
            for(int i = 0; i < recipe.Ingredients.Count; i++) {
                var required = recipe.Ingredients[i];
                if( state.Ingredients.CountFood( required.Item ) < required.Quantity ) {
                    return false;
                }
            }
            return true;
        }

        // Preview shown before "Cook" is pressed; also reused to refresh the label on menu reopen or
        // once a batch finishes. While a batch is in progress it shows what's being made instead.
        public string GetRecipePreviewMessage( CookingStationState state ) {
            if( !state.TargetItem.IsEmpty ) {
                var cookingPortions = Math.Max( state.PendingPortions, 1 );
                var cookingPortionsPrefix = cookingPortions > 1 ? $"{cookingPortions}x " : "";
                return $"Preparing {cookingPortionsPrefix}{DisplayName( state.TargetItem )}...";
            }
            bool hasAnyIngredient = false;
            foreach( var slot in state.Ingredients ) {
                if( !slot.IsEmpty ) {
                    hasAnyIngredient = true;
                    break;
                }
            }
            if( !hasAnyIngredient ) {
                return "";
            }
            var recipe = GetMatchingRecipe( state );
            if( recipe is null ) {
                return NoRecipeMatchMessage;
            }
            var portions = ComputePortionCount( recipe, state.Ingredients );
            var portionsPrefix = portions > 1 ? $"{portions}x " : "";
            return $"You're about to cook {portionsPrefix}{recipe.TargetItem.itemName}.";
        }

        private void UpdateRecipePreview( CookingStation station, CookingStationState state ) {
            RecipePreviewChanged?.Invoke( station, GetRecipePreviewMessage( state ) );
        }

        public bool RegisterCookingStation( CookingStation cookingStation ) {
            var state = new CookingStationState( cookingStation.InitialLevel );
            if( !registeredCookingStations.TryAdd( cookingStation, state ) ) {
                return false;
            }
            state.Ingredients[ 0 ].Changed += slot => OnPotSlotChanged( cookingStation, state, slot );
            foreach( var ingredientSlot in state.Ingredients ) {
                ingredientSlot.Changed += _ => UpdateRecipePreview( cookingStation, state );
            }
            state.FuelSlot.Changed += slot => TryConsumeFuel( state, slot );
            return true;
        }

        public void UnregisterCookingStation( CookingStation cookingStation ) {
            registeredCookingStations.Remove( cookingStation );
            if( currentCookingStation == cookingStation ) {
                currentCookingStation = null;
            }
        }

        private class CookingStationSaveData {
            public CookingStationLevel Level;
            public float FuelLeft;
            public float CookingProgress;
            public float CookTimeSeconds;
            public string TargetItem;
            public NutritionOverride PendingNutrition;
            public int PendingPortions;
        }

        // Ingredient staging slots are intentionally not persisted: a recipe consumes them the moment
        // "Cook" is pressed, so nothing meaningful is sitting in them outside of that single UI action.
        public object CaptureCookingStationState( CookingStation station ) {
            if( !registeredCookingStations.TryGetValue( station, out var state ) ) {
                return null;
            }
            var data = new CookingStationSaveData {
                Level = state.Level,
                FuelLeft = state.FuelLeft,
                CookingProgress = state.CookingProgress,
                CookTimeSeconds = state.CookTimeSeconds,
                TargetItem = state.TargetItem.Value,
                PendingNutrition = state.PendingNutrition,
                PendingPortions = state.PendingPortions,
            };
            return JsonConvert.SerializeObject( data );
        }

        public void RestoreCookingStationState( CookingStation station, string json ) {
            if( json.NullOrEmpty() || !registeredCookingStations.TryGetValue( station, out var state ) ) {
                return;
            }
            var data = JsonConvert.DeserializeObject<CookingStationSaveData>( json );
            if( data is null ) {
                return;
            }
            state.SetLevel( data.Level );
            // Ingredient slots aren't persisted (see the comment on CaptureCookingStationState), but the
            // Pot itself lives in slot 0 as long-lived station equipment, not a consumable ingredient -
            // put it back so it doesn't visually vanish across a save/reload.
            if( data.Level == CookingStationLevel.Pot ) {
                var pot = FindPot();
                if( pot != null ) {
                    state.Ingredients[ 0 ].Set( ItemStack.Of( pot ) );
                }
            }
            state.FuelLeft = data.FuelLeft;
            state.CookingProgress = data.CookingProgress;
            state.CookTimeSeconds = data.CookTimeSeconds;
            state.TargetItem = new ItemId( data.TargetItem );
            state.PendingNutrition = data.PendingNutrition;
            state.PendingPortions = data.PendingPortions;
            state.SetIngredientSlotsLockedForCooking( !state.TargetItem.IsEmpty );
        }
    }
}
