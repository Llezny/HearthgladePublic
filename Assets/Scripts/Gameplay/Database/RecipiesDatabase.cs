using System.Collections.Generic;
using Hearthglade.Gameplay.UI.Menu.Crafting;
using Newtonsoft.Json;
using UnityEngine;

namespace Hearthglade.Gameplay.Database
{
    public class RecipiesDatabase : MonoBehaviour {

        public static RecipiesDatabase instance;
        public TextAsset itemRecipiesJSON;
        public TextAsset buildingsRecipiesJSON;
        public List<Recipe> craftingDatabase;
        public List<Recipe> buildingsDatabase;

        void Awake(){
            if(instance == null)
                instance = this;
            else
                GameObject.Destroy(this);

            buildingsDatabase = JsonConvert.DeserializeObject<List<Recipe>>(buildingsRecipiesJSON.text); 
            craftingDatabase =  JsonConvert.DeserializeObject<List<Recipe>>(itemRecipiesJSON.text); 
        }
    }
}
