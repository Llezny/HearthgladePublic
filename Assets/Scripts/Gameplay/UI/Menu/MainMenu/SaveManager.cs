using System.Collections.Generic;
using System.IO;
using Hearthglade.Gameplay.Common;
using Hearthglade.Gameplay.Map;
using Newtonsoft.Json;
using UnityEngine;


namespace Hearthglade.Gameplay.UI.Menu.MainMenu {
    public class SaveManager {
        
        public static readonly string SAVE_FILE_NAME = "Saves";
        public static readonly string SAVE_FILE_EXTENSION = ".json";
        public static readonly string CONTENT_FILE_NAME = $"content{SAVE_FILE_EXTENSION}";
        public static readonly string HEADER_FILE_NAME = $"header{SAVE_FILE_EXTENSION}";
        
        // Chosen on the new-game screen and stored in the save header; every map's terrain derives from it.
        public int GameSeed => saveDataContainer.SaveHeader.Seed;

        public string PlayerName => saveDataContainer.SaveHeader.PlayerName ?? "";
        public string SavePath => Path.Combine( Application.persistentDataPath, SAVE_FILE_NAME, PlayerName );
        public string ContentPath => Path.Combine( SavePath, CONTENT_FILE_NAME );
        public string HeaderPath => Path.Combine( SavePath, HEADER_FILE_NAME );

        private HashSet<ISaveable> registeredSavables = new();

        private Dictionary<string, object> contentDictionary = new( );
        private SaveDataContainer saveDataContainer;

        public SaveManager() {
            saveDataContainer = ResourceLoader.LoadSaveContainer( );
            if( !saveDataContainer.SaveHeader.NewGame ) {
                LoadData();
            }
        }

        public void RegisterISavable( ISaveable savable ) {
            registeredSavables.Add( savable );
        }
        
        public bool TryGetState<T>( out object value ) {
            return contentDictionary.TryGetValue( typeof( T ).Name, out value);
        }

        public void SaveGame() {
            CaptureStates();
            Directory.CreateDirectory( SavePath );
            
            saveDataContainer.SaveHeader.NewGame = false;
            
            SaveData( HeaderPath, saveDataContainer.SaveHeader, Formatting.Indented );
            // Content is machine-only and large (every block of every map): indentation alone doubled its size.
            SaveData( ContentPath, contentDictionary, Formatting.None );
        }

        private void CaptureStates() {
            contentDictionary = new ();
            foreach(var s in registeredSavables) {
                contentDictionary.Add( s.GetType().Name, s.CaptureState() );
            }
        }

        private void SaveData<T>( string path, T data, Formatting formatting ) {
            File.WriteAllText(path, JsonConvert.SerializeObject( data, formatting ) );
            UnityEngine.Debug.Log($"Saved data to: {path}" );
        }

        private void LoadData() {
            string contentJson = File.ReadAllText( ContentPath );
            string headerJson = File.ReadAllText( HeaderPath );
            contentDictionary = JsonConvert.DeserializeObject<Dictionary<string,object>>(contentJson);
            saveDataContainer.SaveHeader = JsonConvert.DeserializeObject<SaveHeader>(headerJson);
        }
    }
}
