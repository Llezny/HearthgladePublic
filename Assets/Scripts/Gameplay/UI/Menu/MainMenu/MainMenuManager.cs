using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Helpers;
using Hearthglade.Gameplay.Helpers.Extensions;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using Hearthglade.Gameplay.Common.SceneLoader;
using UnityEngine.UI;


namespace Hearthglade.Gameplay.UI.Menu.MainMenu {

    public class MainMenuManager : MonoBehaviour {

        [ SerializeField ] private TextMeshProUGUI playerNamePlaceholder;
        [ SerializeField ] private TextMeshProUGUI playerNameInput;

        [ SerializeField ] private TextMeshProUGUI seedText;
        [ SerializeField ] private RectTransform   loadGameRectTransform;
        [ SerializeField ] private RectTransform   newGameRectTransform;
        [ SerializeField ] private RectTransform   mainMenuRectTransform;

        [ SerializeField ] private TextMeshProUGUI continueDayText;
        [ SerializeField ] private TextMeshProUGUI continueSaveNameText;
        [ SerializeField ] private CanvasGroup     continueButtonGroup;
        [ SerializeField ] private Button continueButton;
        [ SerializeField ] private Button gamesListButton;
        [ SerializeField ] private TextMeshProUGUI gamesListButtonText;

        [ SerializeField ] private TextMeshProUGUI versionText;

        [ SerializeField ] private SaveDataContainer saveDataContainer;

        const float DisabledContinueAlpha = 0.4f;

        public Action<List<SaveHeader>> OnSaveHeadersLoaded;

        private void Update() {
            if (Input.GetKeyUp(KeyCode.Escape)){
                GoToMainMenu();
            }
        }

        private void ContinueGame( SaveHeader saveHeader ) {
            var saveData = ResourceLoader.LoadSaveContainer( );
            saveData.SaveHeader = saveHeader;
            LoadGame( );
        }

        private void OnEnable( ) {
            OnSaveHeadersLoaded += ShowContinueButtonIfAnySaveExists;
        }

        private void OnDisable( ) {
            OnSaveHeadersLoaded -= ShowContinueButtonIfAnySaveExists;
        }

        private void Start( ) {
            LoadSaveHeaders( );
            if( versionText != null ) {
                versionText.text = $"v{Application.version}";
            }
        }

        private void LoadSaveHeaders( ) {
            var headers = new List<SaveHeader>( );
            
            string savePath = Path.Combine( Application.persistentDataPath, SaveManager.SAVE_FILE_NAME );
            var di =  Directory.CreateDirectory( savePath );
            
            var saveDirectories = di.GetDirectories( );
            Array.Sort(saveDirectories, (x, y) => StringComparer.OrdinalIgnoreCase.Compare(y.LastWriteTime,x.LastWriteTime));

            foreach ( var saveDirectory in saveDirectories ) {
                var files = saveDirectory.GetFiles( SaveManager.HEADER_FILE_NAME );
                foreach ( var fileName in files ) {
                    string json = File.ReadAllText( fileName.FullName );
                    headers.Add(JsonConvert.DeserializeObject<SaveHeader>( json ));
                }
            }
            if( gamesListButtonText != null ) {
                gamesListButtonText.text = $"GAMES LIST ({headers.Count} Slots)";
            }
            OnSaveHeadersLoaded( headers );
        }

        // Continue stays visible but greyed out (not hidden) when there is no save to resume, per design.
        private void ShowContinueButtonIfAnySaveExists( List<SaveHeader> saveHeaders ) {
            continueButton.onClick.RemoveAllListeners();
            if ( saveHeaders.Count > 0 ) {
                var save = saveHeaders[ 0 ];
                continueButton.interactable = true;
                if( continueButtonGroup != null ) { continueButtonGroup.alpha = 1f; }
                if( continueDayText != null ) { continueDayText.text = $"DAY {save.DaysCounter}"; }
                if( continueSaveNameText != null ) { continueSaveNameText.text = save.PlayerName; }
                continueButton.onClick.AddListener( () => ContinueGame(save) );
                return;
            }
            continueButton.interactable = false;
            if( continueButtonGroup != null ) { continueButtonGroup.alpha = DisabledContinueAlpha; }
            if( continueDayText != null ) { continueDayText.text = ""; }
            if( continueSaveNameText != null ) { continueSaveNameText.text = ""; }
        }

        public void CreateNewGame() {
            
            // HACK It has to be 16bit, not sure why, but perlin noise map generator can't handle bigger seeds =(
            short newSeed = string.IsNullOrEmpty( seedText.text ) ?
                ( short ) ( UnityEngine.Random.Range( 1, short.MaxValue ) % DateTime.Now.Millisecond + 1 ) :
                ( short ) seedText.text.GetHashCode();

           
            var playerName = playerNamePlaceholder.enabled ? playerNamePlaceholder.text : playerNameInput.text;

            saveDataContainer.SaveHeader = new SaveHeader(){
                NewGame = true,
                PlayerName = playerName,
                Seed = newSeed,
                SaveDate = StringHelper.CurrentDateWithTime
            };

            UnityEngine.Random.InitState( newSeed );
            SceneLoader.Instance.LoadScene( "Game" );
            
        }

        public void LoadGame(){
            SceneLoader.Instance.LoadScene( "Game" );
        }
    

        public void OpenLoadGameMenu(){
            loadGameRectTransform.DOAnchorPos(new Vector2(0,0),0.5f);
            mainMenuRectTransform.DOAnchorPos(new Vector2(0,-3000),0.5f);
        }

        public void OpenNewGameMenu(){
            newGameRectTransform.DOAnchorPos(new Vector2(0,0),0.5f);
            mainMenuRectTransform.DOAnchorPos(new Vector2(0,-3000),0.5f);
            playerNamePlaceholder.text = $"Game-{StringHelper.CurrentDateWithTime}";
        }

        public void GoToMainMenu(){
            loadGameRectTransform.DOAnchorPos(new Vector2(0,3000),0.5f);
            newGameRectTransform.DOAnchorPos(new Vector2(0,3000),0.5f);
            mainMenuRectTransform.DOAnchorPos(new Vector2(0,0),0.5f);
        }

        public void QuitGame(){
            Application.Quit();
        }

    }
}
