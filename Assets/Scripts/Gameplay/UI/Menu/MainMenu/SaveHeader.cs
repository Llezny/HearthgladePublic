using System;

namespace Hearthglade.Gameplay.UI.Menu.MainMenu {
    
    [ Serializable ]
    public class SaveHeader {
        public string PlayerName = "name";
        public string SaveDate = "";
        public bool NewGame;
        public int Seed;
        public int DaysCounter;

        public SaveHeader() {}
        public SaveHeader( SaveHeader save ) {
            PlayerName = save.PlayerName;
            Seed = save.Seed;
            SaveDate = save.SaveDate;
            NewGame = save.NewGame;
            DaysCounter = save.DaysCounter;
        }
    }
}