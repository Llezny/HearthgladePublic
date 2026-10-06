using System.Collections.Generic;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.UI.HUD;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthglade.Gameplay.UI.Menu.MainMenu
{
    public class SaveMenuItem : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI title = null;
        [SerializeField] TextMeshProUGUI day = null;
        [SerializeField] TextMeshProUGUI date = null;

        public void UpdateUI( SaveHeader saveHeader ) {
            title.text   = saveHeader.PlayerName;
            date.text    = saveHeader.SaveDate;
            day.text = saveHeader.DaysCounter.ToString();
        }

        public void SetSaveInfo( ) {
            var loadGameButton = GameObject.Find( "LoadGameButton" ).GetComponent<Button>( );
            var saveData = ResourceLoader.LoadSaveContainer( );
            saveData.SaveHeader = new SaveHeader( ) {
                PlayerName = title.text,
                Seed = 0,
                NewGame = false
            };
            loadGameButton.interactable = true;
        }
    }
}
