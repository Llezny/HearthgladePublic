using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment.Cooking;
using Hearthglade.Gameplay.Items.BuildableItems;
using Hearthglade.Gameplay.Items.UsableItems;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.UI.Menu.Cooking;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Hearthglade.PlayModeTests {

    /// <summary>
    /// Boots the real Game scene and drives a spawned Fireplace CookingStation through CookingService
    /// directly (bypassing the drag-drop UI, same way RecipeMenuIntegrationTests bypasses crafting UI for
    /// setup): staging ingredients/fuel by mutating the station's own ItemSlot instances still
    /// goes through the exact OnChanged events the real UI relies on, so it exercises production code paths.
    /// Run with: Unity -runTests -testPlatform PlayMode (the Editor must be closed).
    /// </summary>
    public class CookingIntegrationTests {

        private static async UniTask<GameplayScope> BootNewGame() {
            var container = ResourceLoader.LoadSaveContainer();
            container.SaveHeader = new SaveHeader { NewGame = true, PlayerName = "cooking-test", Seed = 12345, SaveDate = "" };
            await SceneManager.LoadSceneAsync( "Game" );
            var scope = UnityEngine.Object.FindFirstObjectByType<GameplayScope>();
            Assert.NotNull( scope, "GameplayScope not found in the Game scene" );
            var mapManager = scope.Container.Resolve<MapManager>();
            await WaitUntil( () => mapManager.IsMapReady, 120f, "MapManager.IsMapReady" );
            return scope;
        }

        private static async UniTask WaitUntil( Func<bool> condition, float timeoutSeconds, string what ) {
            float start = Time.realtimeSinceStartup;
            while( !condition() ) {
                if( Time.realtimeSinceStartup - start > timeoutSeconds ) {
                    Assert.Fail( $"Timed out after {timeoutSeconds}s waiting for: {what}" );
                }
                await UniTask.Yield();
            }
        }

        private static ItemDefinition Def( GameplayScope scope, string name ) {
            Assert.IsTrue( scope.Container.Resolve<ItemCatalog>().TryGet( new ItemId( name ), out var item ), $"item {name} not found in the catalog" );
            return item;
        }

        private static CookingStation SpawnFireplace( References references, GameObjectFactory factory ) {
            var fireplaceItem = references.Catalog.GetAsset( new ItemId( "Fireplace" ) ) as BuildableItemSO;
            Assert.NotNull( fireplaceItem, "Fireplace item not found in the database" );
            var instance = factory.Get( fireplaceItem.buildingPrefab );
            var station = instance.GetComponent<CookingStation>();
            Assert.NotNull( station, "Fireplace prefab has no CookingStation component" );
            return station;
        }

        private static CookingStationState Activate( CookingService cookingService, CookingStation station ) {
            CookingStationState state = null;
            void Capture( CookingStation s, CookingStationState st ) => state = st;
            cookingService.ActiveCookingStationChanged += Capture;
            cookingService.SetActiveCookingStation( station );
            cookingService.ActiveCookingStationChanged -= Capture;
            Assert.NotNull( state, "SetActiveCookingStation did not report a state" );
            return state;
        }

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Cooking_TierOneRecipe_ProducesADishInTheOutputSlot_WithQualityScaledStats( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var factory = scope.Container.Resolve<GameObjectFactory>();
            var cookingService = scope.Container.Resolve<CookingService>();

            var station = SpawnFireplace( references, factory );
            var state = Activate( cookingService, station );

            var recipe = references.CookingRecipeDatabase.Find( r => r.name == "BakedCarrot" );
            Assert.NotNull( recipe, "BakedCarrot recipe not found - was it removed from Assets/ScriptableObjects/CookingRecipes?" );

            var carrot = references.Catalog.GetAsset( new ItemId( "Carrot" ) ) as FoodItemSO;
            var wood = Def( scope, "Wood" );
            Assert.NotNull( carrot, "Carrot should be a FoodItemSO" );
            Assert.Greater( carrot.NutritionQuality, 0f, "Carrot needs a NutritionQuality to scale the dish's stats" );

            // Only the first Wood ignites immediately; the rest reignite one at a time as each burns out.
            state.FuelSlot.Set( ItemStack.Of( wood, 3 ) );
            Assert.AreEqual( 1f, state.FuelLeft, "only the first Wood (FuelValue 1) should ignite immediately" );

            state.Ingredients[ 0 ].Set( ItemStack.Of( Def( scope, "Carrot" ), 1 ) );
            cookingService.StartCooking();

            Assert.AreEqual( recipe.TargetItem.Id, state.TargetItem, "the matching recipe should start cooking immediately" );
            Assert.IsTrue( state.Ingredients[ 0 ].IsEmpty, "the ingredient is consumed as soon as cooking starts" );
            Assert.IsTrue( state.Ingredients[ 0 ].IsLocked, "the ingredient slot should be locked while a batch is cooking" );
            Assert.AreEqual( $"Preparing {recipe.TargetItem.itemName}...", cookingService.GetRecipePreviewMessage( state ) );

            await WaitUntil( () => state.OutputSlot.Stack.Count > 0, recipe.BaseTimeToCookInSeconds + 10f, "the dish to land in the output slot" );

            Assert.AreEqual( recipe.TargetItem.Id, state.OutputSlot.Stack.Id );
            Assert.IsTrue( state.OutputSlot.Stack.HasNutritionOverride, "a cooked dish's stats should be an instance override, not the ItemSO's static values" );
            var dish = (IUsableItem) recipe.TargetItem;
            Assert.AreEqual( dish.HungerHealing * carrot.NutritionQuality, state.OutputSlot.Stack.Nutrition.Hunger, 0.01f );
            Assert.AreEqual( dish.ThirstHealing * carrot.NutritionQuality, state.OutputSlot.Stack.Nutrition.Thirst, 0.01f );
            Assert.AreEqual( dish.HealthHealing * carrot.NutritionQuality, state.OutputSlot.Stack.Nutrition.Health, 0.01f );
            Assert.AreEqual( 0f, state.CookingProgress, "progress resets once the dish is delivered" );
            Assert.IsTrue( state.TargetItem.IsEmpty, "TargetItem clears once the dish is delivered" );
            Assert.IsFalse( state.Ingredients[ 0 ].IsLocked, "the ingredient slot should unlock once the batch is delivered" );

            Lean.Pool.LeanPool.Despawn( station.gameObject );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator StartCooking_WithEnoughIngredientsForSeveralPortions_CooksAllOfThemInOneLongerBatch( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var factory = scope.Container.Resolve<GameObjectFactory>();
            var cookingService = scope.Container.Resolve<CookingService>();

            var station = SpawnFireplace( references, factory );
            var state = Activate( cookingService, station );

            var recipe = references.CookingRecipeDatabase.Find( r => r.name == "BakedCarrot" );
            Assert.NotNull( recipe, "BakedCarrot recipe not found" );

            var carrot = Def( scope, "Carrot" );
            var wood = Def( scope, "Wood" );

            state.FuelSlot.Set( ItemStack.Of( wood, 10 ) );
            state.Ingredients[ 0 ].Set( ItemStack.Of( carrot, 5 ) );
            cookingService.StartCooking();

            Assert.AreEqual( recipe.BaseTimeToCookInSeconds * 5, state.CookTimeSeconds, "cook time should scale with the portion count" );
            Assert.IsTrue( state.Ingredients[ 0 ].IsEmpty, "all 5 Carrots make exactly 5 whole portions" );
            Assert.AreEqual( $"Preparing 5x {recipe.TargetItem.itemName}...", cookingService.GetRecipePreviewMessage( state ) );

            await WaitUntil( () => state.OutputSlot.Stack.Count > 0, state.CookTimeSeconds + 10f, "the batch to finish cooking" );

            Assert.AreEqual( 5, state.OutputSlot.Stack.Count, "5 Carrots should produce 5 dishes in one batch" );
            Assert.AreEqual( 0f, state.CookingProgress, "progress should reset once the batch is delivered" );

            Lean.Pool.LeanPool.Despawn( station.gameObject );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator FuelSlot_BurnsOneItemAtATimeFromTheStack_ReignitingOnlyOnceTheCurrentOneBurnsOut( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var factory = scope.Container.Resolve<GameObjectFactory>();
            var cookingService = scope.Container.Resolve<CookingService>();

            var station = SpawnFireplace( references, factory );
            var state = Activate( cookingService, station );
            var wood = Def( scope, "Wood" );

            // Regression test: dropping a full stack used to burn it all into a shared fuel pool at once.
            state.FuelSlot.Set( ItemStack.Of( wood, 10 ) );

            Assert.AreEqual( 1f, state.FuelLeft, "only the first Wood (FuelValue 1) should ignite immediately" );
            Assert.AreEqual( 9, state.FuelSlot.Stack.Count, "the other 9 should stay in the slot as reserve" );

            // Force the current item to run out and let Tick() pull the next one in automatically.
            state.AddFuel( -1f );
            await WaitUntil( () => state.FuelSlot.Stack.Count == 8, 5f, "the next Wood to ignite once the previous one burns out" );

            Assert.Greater( state.FuelLeft, 0.9f, "the newly-ignited Wood should be burning at close to full value again" );

            Lean.Pool.LeanPool.Despawn( station.gameObject );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator FuelSlot_DroppedViaARealDragDropMove_StillReflectsTheImmediateIgnition( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var factory = scope.Container.Resolve<GameObjectFactory>();
            var cookingService = scope.Container.Resolve<CookingService>();

            var station = SpawnFireplace( references, factory );
            var state = Activate( cookingService, station );
            var wood = Def( scope, "Wood" );

            // Regression test: MoveItemToEmptySlot used to undo the ignition's decrement after Set().
            var draggedStack = new ItemSlot();
            draggedStack.Set( ItemStack.Of( wood, 10 ) );
            ItemTransfer.Move( draggedStack, state.FuelSlot );

            Assert.AreEqual( 1f, state.FuelLeft, "the first Wood should still ignite via a real drag-drop move" );
            Assert.AreEqual( 9, state.FuelSlot.Stack.Count, "the ignited Wood must not be silently un-consumed by the move" );
            Assert.IsTrue( draggedStack.IsEmpty, "the whole dragged stack should have left the source slot" );

            Lean.Pool.LeanPool.Despawn( station.gameObject );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator Pot_DroppedIntoTheFireplaceSlot_UpgradesStationAndUnlocksSlots( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var factory = scope.Container.Resolve<GameObjectFactory>();
            var cookingService = scope.Container.Resolve<CookingService>();

            var station = SpawnFireplace( references, factory );
            var state = Activate( cookingService, station );
            var pot = Def( scope, "Pot" );
            Assert.NotNull( pot, "Pot item not found in the database" );

            Assert.AreEqual( CookingStationLevel.Fireplace, state.Level );
            Assert.IsFalse( state.Ingredients[ 0 ].IsLocked, "slot 0 is always usable" );
            Assert.IsTrue( state.Ingredients[ 1 ].IsLocked, "only 1 slot is usable at Fireplace level" );

            var upgraded = false;
            void OnUpgraded( CookingStation s, CookingStationState st ) => upgraded = true;
            cookingService.StationUpgraded += OnUpgraded;

            state.Ingredients[ 0 ].Set( ItemStack.Of( pot, 1 ) );

            cookingService.StationUpgraded -= OnUpgraded;
            Assert.IsTrue( upgraded, "StationUpgraded should fire when a Pot is dropped on a Fireplace" );
            Assert.AreEqual( CookingStationLevel.Pot, state.Level );
            Assert.AreEqual( new ItemId( "Pot" ), state.Ingredients[ 0 ].Stack.Id, "the Pot stays put as station equipment instead of being consumed" );
            for( int i = 0; i < CookingStationLevelExtensions.MaxIngredientSlots; i++ ) {
                Assert.IsFalse( state.Ingredients[ i ].IsLocked, $"slot {i} should unlock at Pot level" );
            }

            var potVisual = station.transform.Find( "PotVisual" );
            Assert.NotNull( potVisual, "Fireplace prefab should have a PotVisual child for the world model" );
            Assert.IsTrue( potVisual.gameObject.activeSelf, "the Pot model should appear in the world once upgraded" );

            Lean.Pool.LeanPool.Despawn( station.gameObject );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator DraggingThePotOutOfItsSlot_DowngradesTheStationBackToFireplace( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var factory = scope.Container.Resolve<GameObjectFactory>();
            var cookingService = scope.Container.Resolve<CookingService>();

            var station = SpawnFireplace( references, factory );
            var state = Activate( cookingService, station );
            var pot = Def( scope, "Pot" );

            state.Ingredients[ 0 ].Set( ItemStack.Of( pot, 1 ) );
            Assert.AreEqual( CookingStationLevel.Pot, state.Level, "dropping the Pot in should upgrade the station" );

            var downgraded = false;
            void OnChanged( CookingStation s, CookingStationState st ) => downgraded = true;
            cookingService.StationUpgraded += OnChanged;

            // A real drag-drop move out of slot 0, not a direct CleanSlot() call.
            var playerHand = new ItemSlot();
            ItemTransfer.Move( state.Ingredients[ 0 ], playerHand );

            cookingService.StationUpgraded -= OnChanged;
            Assert.IsTrue( downgraded, "StationUpgraded should also fire on downgrade so the world model refreshes" );
            Assert.AreEqual( CookingStationLevel.Fireplace, state.Level, "removing the Pot should downgrade the station" );
            Assert.AreEqual( new ItemId( "Pot" ), playerHand.Stack.Id, "the Pot should end up wherever the player dragged it" );
            Assert.IsTrue( state.Ingredients[ 1 ].IsLocked, "the 3 bonus slots should lock again once downgraded" );

            var potVisual = station.transform.Find( "PotVisual" );
            Assert.IsFalse( potVisual.gameObject.activeSelf, "the Pot model should disappear from the world once downgraded" );

            Lean.Pool.LeanPool.Despawn( station.gameObject );
        } );

        [ UnityTest, Timeout( 300000 ) ]
        public IEnumerator StartCooking_RejectsRecipesWithTooHighATierOrNotEnoughIngredients( ) => UniTask.ToCoroutine( async ( ) => {
            var scope = await BootNewGame();
            var references = scope.Container.Resolve<References>();
            var factory = scope.Container.Resolve<GameObjectFactory>();
            var cookingService = scope.Container.Resolve<CookingService>();

            var station = SpawnFireplace( references, factory );
            var state = Activate( cookingService, station );

            var carrot = Def( scope, "Carrot" );
            var wood = Def( scope, "Wood" );
            var bakedCarrot = references.Catalog.GetAsset( new ItemId( "BakedCarrot" ) );

            // Two decoy recipes that should each fail to match for a different reason, both producing the
            // same item as the real BakedCarrot recipe so the only way to tell them apart is CookTimeSeconds.
            var potTierRecipe = ScriptableObject.CreateInstance<CookingRecipeSO>();
            potTierRecipe.TargetItem = bakedCarrot;
            potTierRecipe.MinCookingStationLevel = CookingStationLevel.Pot;
            potTierRecipe.BaseTimeToCookInSeconds = 1f;
            potTierRecipe.Ingredients = new List<Ingredient> { new Ingredient { Item = FoodType.Vegetable, Quantity = 1 } };

            var needsTwoRecipe = ScriptableObject.CreateInstance<CookingRecipeSO>();
            needsTwoRecipe.TargetItem = bakedCarrot;
            needsTwoRecipe.MinCookingStationLevel = CookingStationLevel.Fireplace;
            needsTwoRecipe.BaseTimeToCookInSeconds = 1f;
            needsTwoRecipe.Ingredients = new List<Ingredient> { new Ingredient { Item = FoodType.Vegetable, Quantity = 2 } };

            references.CookingRecipeDatabase.Insert( 0, potTierRecipe );
            references.CookingRecipeDatabase.Insert( 0, needsTwoRecipe );
            try {
                state.FuelSlot.Set( ItemStack.Of( wood, 5 ) );
                state.Ingredients[ 0 ].Set( ItemStack.Of( carrot, 1 ) );

                cookingService.StartCooking();

                Assert.AreEqual( new ItemId( "BakedCarrot" ), state.TargetItem,
                    "with only 1 Carrot on a Fireplace, both decoys must be skipped and the real 1-Vegetable/Fireplace recipe used" );
                Assert.AreEqual( 10f, state.CookTimeSeconds,
                    "CookTimeSeconds should come from the real BakedCarrot recipe (10s), not either 1s decoy" );
            }
            finally {
                references.CookingRecipeDatabase.Remove( potTierRecipe );
                references.CookingRecipeDatabase.Remove( needsTwoRecipe );
                UnityEngine.Object.Destroy( potTierRecipe );
                UnityEngine.Object.Destroy( needsTwoRecipe );
            }

            Lean.Pool.LeanPool.Despawn( station.gameObject );
        } );
    }
}
