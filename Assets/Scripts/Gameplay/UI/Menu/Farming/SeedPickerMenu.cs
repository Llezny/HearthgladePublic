using System.Collections.Generic;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.Environment.Farming;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Farming
{
    // Lists the seeds the player owns that fit the plot being planted. The tiles are created once and reused.
    public class SeedPickerMenu : Common.Menu {

        private const string TilePrefabAddress = "UIPrefabs/Menu/PlantMenu/PlantTile";

        [ SerializeField ] GameObject plantTile = null;
        [ SerializeField ] Transform plantTileList = null;
        [ SerializeField ] Button closeButton;
        [ Tooltip( "Optional: shown when the player has no seed that fits." ) ]
        [ SerializeField ] GameObject emptyHint = null;

        // Dependencies
        private FarmingService farmingService;
        private ItemCatalog catalog;
        //

        private readonly List<SeedTile> tiles = new();
        private Plot currentPlot;
        private AsyncOperationHandle<GameObject> tilePrefabHandle;
        private bool isLoadingTilePrefab;

        [ Inject ]
        public void Construct( FarmingService farmingService, ItemCatalog catalog ) {
            this.farmingService = farmingService;
            this.catalog = catalog;
        }

        private void OnEnable() {
            closeButton.onClick.AddListener( () => CloseMenu() );
        }

        private void OnDisable() {
            closeButton.onClick.RemoveAllListeners();
        }

        private void OnDestroy() {
            if( tilePrefabHandle.IsValid() ) {
                Addressables.Release( tilePrefabHandle );
            }
        }

        public void Open( Plot plot ) {
            base.OpenMenu();
            currentPlot = plot;
            Refresh();
        }

        public void Plant( CropSO crop ) {
            if( currentPlot == null ) {
                return;
            }
            if( farmingService.Plant( currentPlot.Key, crop ) == Core.Farming.PlantResult.Planted ) {
                CloseMenu();
                return;
            }
            Refresh();
        }

        private void Refresh() {
            if( plantTile != null ) {
                Rebuild();
                return;
            }
            if( isLoadingTilePrefab ) {
                return;
            }
            isLoadingTilePrefab = true;
            tilePrefabHandle = Addressables.LoadAssetAsync<GameObject>( TilePrefabAddress );
            tilePrefabHandle.Completed += handle => {
                isLoadingTilePrefab = false;
                plantTile = handle.Result;
                if( isOpen ) {
                    Rebuild();
                }
            };
        }

        private void Rebuild() {
            int shown = 0;
            foreach( var crop in farmingService.Crops.All ) {
                int owned = farmingService.OwnedSeeds( crop );
                if( currentPlot == null || crop.plotType != currentPlot.PlotType || owned < 1 ) {
                    continue;
                }
                if( shown == tiles.Count ) {
                    tiles.Add( Instantiate( plantTile, plantTileList ).GetComponent<SeedTile>() );
                }
                tiles[ shown ].gameObject.SetActive( true );
                tiles[ shown ].SetTile( crop, owned, farmingService.PlantingNote( currentPlot.Key, crop ), this );
                shown++;
            }
            for( int i = shown; i < tiles.Count; i++ ) {
                tiles[ i ].gameObject.SetActive( false );
            }
            if( emptyHint != null ) {
                emptyHint.SetActive( shown == 0 );
            }
        }
    }
}
