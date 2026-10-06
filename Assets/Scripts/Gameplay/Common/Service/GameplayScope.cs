using Hearthglade.Core.Farming;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Audio;
using Hearthglade.Gameplay.Common.Events;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Debug;
using Hearthglade.Gameplay.Debug.Commands;
using Hearthglade.Gameplay.Environment.Cooking;
using Hearthglade.Gameplay.Environment.Farming;
using Hearthglade.Gameplay.Events;
using Hearthglade.Gameplay.Expeditions;
using Hearthglade.Gameplay.Map;
using Hearthglade.Gameplay.Map.Loading;
using Hearthglade.Gameplay.Player;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.Player.Stats;
using Hearthglade.Gameplay.Trade;
using Hearthglade.Gameplay.UI.HUD;
using Hearthglade.Gameplay.UI.HUD.Messages;
using Hearthglade.Gameplay.UI.ItemPickup;
using Hearthglade.Gameplay.UI.Menu;
using Hearthglade.Gameplay.UI.Menu.Build;
using Hearthglade.Gameplay.UI.Menu.BuildBlock;
using Hearthglade.Gameplay.UI.Menu.Common;
using Hearthglade.Gameplay.UI.Menu.Cooking;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using Hearthglade.Gameplay.UI.Menu.DeathScreen;
using Hearthglade.Gameplay.UI.Menu.Farming;
using Hearthglade.Gameplay.UI.Menu.Fishing;
using Hearthglade.Gameplay.UI.Menu.Help;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using Hearthglade.Gameplay.UI.Menu.MainMenu;
using Hearthglade.Gameplay.UI.Menu.Ship;
using Hearthglade.Gameplay.UI.Menu.Trade;
using PrefabLightmapBaker;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Common.Service {
    public class GameplayScope : LifetimeScope {
        
        [ SerializeField ] LoadedMapEvent loadedMapEvent;
        [ SerializeField ] PrefabLightmaps prefabLightmaps;
        [ SerializeField ] StatBarsContainerSO statBarsContainerSO;
        [ SerializeField ] MessagesDictionarySO messagesDictionarySO;

        protected override void Configure( IContainerBuilder builder ) {
            builder.Register<MapGenerator>( Lifetime.Scoped );
            builder.RegisterComponentInHierarchy<GameManager>();
            builder.RegisterComponentInHierarchy<BuildingPlacer>();
            builder.RegisterComponentInHierarchy<MainLight>();
            builder.RegisterComponentInHierarchy<EquipmentComponent>( );
            builder.RegisterComponentInHierarchy<ItemPickup>( );
            builder.RegisterComponentInHierarchy<InteractBehaviour>( );
            builder.RegisterComponentInHierarchy<ClickToInteractBehaviour>( );
            builder.RegisterComponentInHierarchy<PlayerController>( );
            builder.RegisterComponentInHierarchy<ItemEquipper>( );
            builder.RegisterComponentInHierarchy<PlayerStatsComponent>( );

#region Events
            builder.RegisterComponentInHierarchy<GameEvents>();
            builder.RegisterComponentInHierarchy<ControlEvents>();
#endregion Events

#region Database
            builder.RegisterComponentInHierarchy<References>();
            builder.Register<ItemCatalog>( resolver => resolver.Resolve<References>().Catalog, Lifetime.Scoped ).As<IItemCatalog>().AsSelf();
            builder.Register<CropCatalog>( resolver => resolver.Resolve<References>().Crops, Lifetime.Scoped ).As<ICropCatalog>().AsSelf();
#endregion Database

#region Services
            builder.Register<EquipmentService>( Lifetime.Scoped );
            builder.Register<SaveManager>( Lifetime.Scoped );
            builder.Register<ShipService>( Lifetime.Scoped );
            builder.Register<EnvironmentLightningService>( Lifetime.Scoped );
            builder.RegisterEntryPoint<FarmingService>( Lifetime.Scoped ).AsSelf();
            builder.Register<InventoryService>(Lifetime.Scoped).AsSelf().As<IItemSlotActions>();
            builder.RegisterEntryPoint<ItemUseAudio>( Lifetime.Scoped );
            builder.Register<ItemFrameService>(Lifetime.Scoped);
            builder.RegisterEntryPoint<CookingService>( Lifetime.Scoped ).AsSelf();
            builder.RegisterEntryPoint<PortService>( Lifetime.Scoped ).AsSelf();
            builder.RegisterEntryPoint<ExpeditionService>( Lifetime.Scoped ).AsSelf();
            builder.RegisterEntryPoint<ShipTreeService>( Lifetime.Scoped ).AsSelf();
            builder.Register<TradeService>( Lifetime.Scoped );
            // RegisterEntryPoint (not plain Register) is required here: it is what wires MapManager's
            // ITickable into VContainer's dispatch loop. Plain Register left Tick() (chunk streaming,
            // CurrentMap.UpdateChunks()) never called, so chunks stopped streaming after the initial load.
            builder.RegisterEntryPoint<MapManager>( Lifetime.Scoped ).AsSelf();
            builder.Register<RestoreOrCreateMapStep>( Lifetime.Scoped );
            builder.RegisterEntryPoint<GameLoadCoordinator>( Lifetime.Scoped ).AsSelf();
            builder.RegisterComponentInHierarchy<CanvasService>();
            builder.RegisterComponentInHierarchy<AddressableService>();
            builder.RegisterComponentOnNewGameObject<CameraService>( Lifetime.Singleton, "CameraService" );
            // builder.RegisterComponentInHierarchy<JoystickManager>();
            builder.RegisterEntryPoint<TickService>( Lifetime.Scoped ).AsSelf();
            
#endregion Services

#region Factories
            builder.Register<MapFactory>( Lifetime.Singleton );
            builder.Register<GameObjectFactory>( Lifetime.Singleton );
            builder.Register<PlayerStatBarFactory>( Lifetime.Singleton );
            builder.Register<InventorySlotViewFactory>( Lifetime.Singleton ).As<IInventorySlotViewFactory>();
            builder.Register<MessageFactory>( Lifetime.Singleton ).As<IMessageFactory>();
            
#endregion Factories

#region UI
            builder.RegisterComponentInHierarchy<ClockManager>( ).AsSelf( ).As<IWorldClock>( );
            builder.RegisterInstance<ILoadingScreen>( LoadingScreen.Instance );
            builder.RegisterInstance<AudioManager>( AudioManager.Instance );
            builder.RegisterComponentInHierarchy<MessagePopup>();
            builder.RegisterComponentInHierarchy<EquipmentView>( );
            builder.RegisterComponentInHierarchy<ChestView>();
            builder.RegisterComponentInHierarchy<InventoryGridView>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<InventoryMenu>();
            builder.RegisterComponentInHierarchy<Crafting>();
            builder.RegisterComponentInHierarchy<ShipMenu>();
            builder.RegisterComponentInHierarchy<PortMenu>();
            builder.RegisterComponentInHierarchy<ShipTreeMenu>();
            builder.RegisterComponentInHierarchy<CookingMenu>();
            builder.RegisterComponentInHierarchy<BuildingMenu>();
            builder.RegisterComponentInHierarchy<SeedPickerMenu>();
            builder.RegisterComponentInHierarchy<FishingPopup>();
            builder.RegisterComponentInHierarchy<PauseMenu>();
            builder.RegisterComponentInHierarchy<TooltipManager>();
            builder.RegisterComponentInHierarchy<PlayerStatView>();
            builder.RegisterComponentInHierarchy<InteractionIcon>();
            builder.RegisterComponentInHierarchy<BuildBlockMenu>();
            builder.RegisterComponentInHierarchy<MenuManager>();
            builder.RegisterComponentInHierarchy<DeathScreen>();
            builder.RegisterComponentInHierarchy<DamageIndicator>();
            builder.RegisterComponentInHierarchy<HelpScreen>();
            builder.RegisterComponentInHierarchy<BottomBar>();
#endregion UI

#region ScriptableObject
            builder.RegisterInstance<LoadedMapEvent>( loadedMapEvent );
            builder.RegisterInstance<PrefabLightmaps>( prefabLightmaps );
            builder.RegisterInstance<StatBarsContainerSO>( statBarsContainerSO );
            builder.RegisterInstance<MessagesDictionarySO>( messagesDictionarySO );
#endregion ScriptableObject
            

#region Console Commands
            builder.Register<ConsoleCommandsService>( Lifetime.Scoped );
            builder.RegisterEntryPoint<InventoryCommands>( Lifetime.Scoped ).AsSelf();
            builder.RegisterEntryPoint<ClockCommands>( Lifetime.Scoped ).AsSelf();
            builder.RegisterEntryPoint<PortCommands>( Lifetime.Scoped ).AsSelf();
            builder.RegisterEntryPoint<ExpeditionCommands>( Lifetime.Scoped ).AsSelf();
            builder.Register<GraphicsCommands>( Lifetime.Scoped );
            builder.Register<DebugLogService>( Lifetime.Scoped );
            builder.RegisterComponentInHierarchy<DebugConsoleView>();
#endregion Console Commands

#region Debug Menu
            builder.RegisterComponentInHierarchy<DebugMenu>();
#endregion Debug Menu



        }
    }
}
