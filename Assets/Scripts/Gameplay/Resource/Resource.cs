using System;
using DG.Tweening;
using Hearthglade.Gameplay.Audio;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Common.Service.Factory;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Expeditions;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Player;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.HUD;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Resource
{
    public class Resource : SceneObject, IInteractable {

        [ SerializeField ] public ResourceSO resourceSO;

        // Prop variant of a prefab (a Prefab Variant with this ticked): it looks and blocks like the original but cannot be gathered.
        [ SerializeField ] private bool isProp;
        public bool IsProp => isProp;
        [ SerializeField ] private string propRefusalMessage = "The locals wouldn't like me chopping down their trees.";
        public string RefusalMessage => isProp ? propRefusalMessage : tools?.MissingToolMessage( resourceSO );

        [field: SerializeField] public bool IsCurrentlyGathered { get; protected set; }
        public string TooltipTitle => resourceSO.name;
        public string TooltipDescription => resourceSO.ToolTipMessage;
        public float InteractionDistance => resourceSO.MinInteractionDistance;
        public AnimationClip AnimationOverride => resourceSO.AnimationOverride;
        public ResourceSO Gathered => isProp ? null : resourceSO;
        public InteractionTiming InteractionTiming => InteractionTiming.LoadingBar;
        public float InteractionDuration => tools.GatherSeconds( resourceSO );

        protected PlayerController playerController;
        protected PlayerToolService tools;
        protected InventoryService inventoryService;
        protected ShipTreeService shipTree;
        protected TickService tickService;
        protected GameObjectFactory gameObjectFactory;
        protected ClockManager clockManager;
        protected ISfxPlayer sfx;
        protected Action interactionCompleted;

        [ Inject ]
        public void Construct( PlayerController playerController, InventoryService inventoryService, PlayerToolService tools, TickService tickService, GameObjectFactory gameObjectFactory, ClockManager clockManager, ISfxPlayer sfx, ShipTreeService shipTree ) {
            this.playerController = playerController;
            this.inventoryService = inventoryService;
            this.tools = tools;
            this.tickService = tickService;
            this.gameObjectFactory = gameObjectFactory;
            this.clockManager = clockManager;
            this.sfx = sfx;
            this.shipTree = shipTree;
            tickService.RegisterListener(Tick);
        }
        
        protected override void OnEnable() {
            base.OnEnable();
            tickService?.RegisterListener(Tick);
        }

        protected override void OnDisable() {
            base.OnDisable();
            interactionCompleted = null;
            tickService?.UnregisterListener(Tick);
        }

        public override void Tick() {}

        public void InteractCancelCallback( ) {
            IsCurrentlyGathered = false;
            tools.Cancel();
        }

        public override void InteractionStart(){
            tools.Begin( resourceSO );
            clockManager.SetTimeScale( 2 );
            IsCurrentlyGathered = true;
            SetPlayerPosition( playerController.transform );
        }
        
        public override bool CanInteract( ) {
            if( isProp ) {
                return false;
            }
            // A resource that needs a tool is refused until the player carries one (the reason is RefusalMessage).
            bool hasTool() => tools.CanGather( resourceSO );
            // Gathering is refused while the yield would not fit, so nothing is lost.
            bool hasRoom() => resourceSO.ItemSoOnGather == null || inventoryService.CanFit( resourceSO.ItemSoOnGather, resourceSO.NumOfItemsOnGather );
            return hasTool() && hasRoom();
        }

        public void AddInteractionCompletedCallback( Action callback ) {
            interactionCompleted += callback;
        }

        protected void SetPlayerPosition( Transform playerTransform ) {
            playerTransform.LookAt( this.transform );
            playerTransform.rotation = Quaternion.Euler(
                0,
                playerTransform.rotation.eulerAngles.y,
                playerTransform.rotation.eulerAngles.z
            );
        }

        public override void InteractionCompleted() {
            inventoryService.AddItem( resourceSO.ItemSoOnGather, resourceSO.NumOfItemsOnGather );
            // The keen eye of the ship tree: sometimes one more of the yield.
            if( resourceSO.ItemSoOnGather != null && UnityEngine.Random.value < shipTree.HarvestChance ) {
                inventoryService.AddItem( resourceSO.ItemSoOnGather, 1 );
            }
            if( resourceSO.BonusItem != null && UnityEngine.Random.value < resourceSO.BonusChance ) {
                inventoryService.AddItem( resourceSO.BonusItem, 1 );
            }
            tools.Complete();
            sfx.Play( resourceSO.OnPickup );
            clockManager.ResetTimeScale();
            IsCurrentlyGathered = false;
            interactionCompleted?.Invoke();
            Lean.Pool.LeanPool.Despawn( this.gameObject );

        }
    }
}