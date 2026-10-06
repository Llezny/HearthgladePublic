using Hearthglade.Gameplay.Player.Controller;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;

namespace Hearthglade.Gameplay.Items
{
    [CreateAssetMenu(fileName = "Torch", menuName = "ScriptableObjects/Items/Equipment/Torch")]
    public class Torch : ItemSO {
        
        private const string LIGHT_GO_NAME = "PlayerLight";

      //   public override void OnEquip() {
      // //      base.OnEquip();
      // //      var playerGO = GameObject.FindGameObjectWithTag( "Player" );
      // //      playerGO.GetComponent<PlayerController>().onWalk += () => Inventory.instance.DecreaseHandItemDurability( true );
      //  //     CreateLight( playerGO );
      //   }
      //
      //   public override void OnUnequip() {
      //  //     base.OnUnequip();
      //  //     var playerGO = GameObject.FindGameObjectWithTag( "Player" );
      //  //     GameObject.Destroy( playerGO.transform.Find( LIGHT_GO_NAME ).gameObject );
      //  //     playerGO.GetComponent<PlayerController>().onWalk -= () => Inventory.instance.DecreaseHandItemDurability( true );
      //   }

        private void CreateLight( GameObject playerGO ) {
            var obj = new GameObject( LIGHT_GO_NAME );
            obj.transform.SetParent( GameObject.FindGameObjectWithTag( "Player" ).transform );
            obj.transform.localPosition = new Vector3( 0, 1.4f, 0 );
            var lightComponent = obj.AddComponent<Light>();
            lightComponent.color = new Color( 0.78f, 0.42f, 0, 1 );
            lightComponent.type = LightType.Point;
            lightComponent.intensity = 3;
        }

    }
}
