using Hearthglade.Core.Farming;
using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.Resource;
using Hearthglade.Gameplay.UI.Menu.Farming;
using UnityEngine;
using VContainer;

namespace Hearthglade.Gameplay.Environment.Farming
{
    // The view of one garden plot (bed, orchard, trellis). It only knows its key and asks the FarmModel; despawning it
    // with its chunk loses nothing, because the crop lives in Map.Farm.
    public class Plot : SceneObject, IInteractable {

        [ SerializeField ] PlotType plotType = PlotType.Bed;
        [ Tooltip( "Floats above the plot while something is ripe." ) ]
        [ SerializeField ] ActivityBadge readyBadgePrefab = null;
        [ SerializeField ] Sprite readyGlyph = null;
        [ SerializeField ] Vector3 readyBadgeOffset = new( 0f, 0.25f, 0f );

        private FarmingService farming;
        private SeedPickerMenu seedPicker;
        private PlayerController playerController;

        private FarmModel farm;
        private PlotKey key;
        private bool isBound;
        private CropSO crop;
        private GameObject visualObject;
        private CropVisual visual;
        private GameObject readyBadge;

        public PlotType PlotType => plotType;
        public PlotKey Key => key;

        // World minute at which the view has to be redrawn because the stage or the fruit changes; MaxValue = never.
        public long NextRefreshAt { get; private set; } = long.MaxValue;

        private PlotView View => isBound ? farm.Describe( key, farming.Now ) : PlotView.Missing;
        private bool IsRipe => View.ReadyYield > 0;

        public string TooltipTitle => crop != null ? crop.DisplayName : "Garden patch";
        public string TooltipDescription => Describe( View, crop != null ? farming.ClimateFor( key, crop ).issue : ClimateIssue.None );
        public float InteractionDistance => crop != null && crop.harvest != null ? crop.harvest.MinInteractionDistance : 0.4f;
        public float InteractionDuration => farming.GatheringSeconds( crop );
        public AnimationClip AnimationOverride => Gathered != null ? Gathered.AnimationOverride : null;
        // Only a ripe crop is gathered; tending the patch (planting, looking) plays no work animation.
        public ResourceSO Gathered => IsRipe && crop != null ? crop.harvest : null;
        public InteractionTiming InteractionTiming => IsRipe ? InteractionTiming.LoadingBar : InteractionTiming.Instant;

        [ Inject ]
        public void Construct( FarmingService farming, SeedPickerMenu seedPicker, PlayerController playerController ) {
            this.farming = farming;
            this.seedPicker = seedPicker;
            this.playerController = playerController;
        }

        // Spawned from a chunk or placed by the player: now the cell is known, so the crop can be looked up.
        protected override void OnSceneObjectModelAssigned() {
            Bind();
        }

        protected override void OnDisable() {
            base.OnDisable();
            // The children go inactive with us and cannot be re-parented now; the next Bind releases them.
            Unbind( releaseVisual: false );
        }

        private void Bind() {
            Unbind();
            farm = farming?.Farm;
            if( farm == null ) {
                return;
            }
            var cell = Hearthglade.Gameplay.Map.MapHelper.WorldPositionToBlockIndex( transform.position );
            key = new PlotKey( cell.x, 0, cell.y );
            // Beds from saves that predate the farm model have no record yet.
            farm.EnsurePlot( key, plotType );
            farm.PlotChanged += OnPlotChanged;
            farming.Register( this );
            isBound = true;
            Refresh();
        }

        private void Unbind( bool releaseVisual = true ) {
            if( isBound ) {
                farm.PlotChanged -= OnPlotChanged;
                farming.Unregister( this );
            }
            isBound = false;
            farm = null;
            crop = null;
            if( releaseVisual ) {
                ClearVisual();
            }
            NextRefreshAt = long.MaxValue;
        }

        private void OnPlotChanged( PlotKey changed ) {
            if( changed == key ) {
                Refresh();
            }
        }

        public void Refresh() {
            if( !isBound ) {
                return;
            }
            long now = farming.Now;
            var view = farm.Describe( key, now );
            if( !view.Exists || view.IsEmpty ) {
                crop = null;
                ClearVisual();
            } else {
                if( crop == null || crop.Id != view.CropId ) {
                    ClearVisual();
                    farming.Crops.TryGetAsset( view.CropId, out crop );
                    SpawnVisual();
                }
                if( visual != null ) {
                    visual.Apply( view );
                }
            }
            SetReadyBadge( view.ReadyYield > 0 );
            NextRefreshAt = farm.NextChangeAt( key, now ) ?? long.MaxValue;
        }

        private void SpawnVisual() {
            if( crop == null || crop.visualPrefab == null ) {
                return;
            }
            visualObject = Lean.Pool.LeanPool.Spawn( crop.visualPrefab, transform.position, transform.rotation, transform );
            visual = visualObject.GetComponent<CropVisual>();
            if( visual != null ) {
                visual.Orient();
            }
        }

        private void SetReadyBadge( bool ripe ) {
            if( ripe && readyBadge == null && readyBadgePrefab != null ) {
                readyBadge = Lean.Pool.LeanPool.Spawn( readyBadgePrefab.gameObject, transform.position + readyBadgeOffset, Quaternion.identity, transform );
                readyBadge.GetComponent<ActivityBadge>().SetGlyph( readyGlyph );
            } else if( !ripe && readyBadge != null ) {
                Lean.Pool.LeanPool.Despawn( readyBadge );
                readyBadge = null;
            }
        }

        private void ClearVisual() {
            SetReadyBadge( false );
            if( visualObject != null ) {
                Lean.Pool.LeanPool.Despawn( visualObject );
            }
            visualObject = null;
            visual = null;
        }

        private static string Describe( PlotView view, ClimateIssue issue ) {
            if( !view.Exists || view.IsEmpty ) {
                return "Tap to plant";
            }
            if( view.ReadyYield > 0 ) {
                return "Ready to harvest";
            }
            float progress = ( view.Stage + view.Progress ) / Mathf.Max( 1, view.StageCount );
            string slow = view.SpeedPercent < 100 && issue != ClimateIssue.None ? $" (slow: {FarmingService.IssueText( issue )})" : "";
            return $"Growing {Mathf.FloorToInt( progress * 100f )}% - ripe in {FarmingService.FormatRealTime( view.MinutesUntilRipe )}{slow}";
        }

        public override bool CanInteract() {
            var view = View;
            if( !view.Exists ) {
                return false;
            }
            if( view.IsEmpty ) {
                return true;
            }
            return farming.CanHarvest( key, crop );
        }

        public override void InteractionStart() {
            if( !isBound ) {
                return;
            }
            if( View.IsEmpty ) {
                seedPicker.Open( this );
                return;
            }
            farming.BeginGathering( crop );
            playerController.transform.LookAt( transform );
            var angles = playerController.transform.rotation.eulerAngles;
            playerController.transform.rotation = Quaternion.Euler( 0, angles.y, angles.z );
        }

        public void InteractCancelCallback() {
            farming.EndGathering();
        }

        public override void InteractionCompleted() {
            bool harvested = isBound && farming.Harvest( key, crop );
            farming.EndGathering( harvested );
            if( harvested ) {
                // A puff of dust over the bed where the produce came off.
                PlaceDust.Spawn( transform.position + readyBadgeOffset * 0.1f, transform.rotation, new Vector2( 0.3f, 0.3f ) );
            }
        }
    }
}
