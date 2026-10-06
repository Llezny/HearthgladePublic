

using UnityEngine;

namespace Hearthglade.Gameplay.Player.Stats {
    [CreateAssetMenu(fileName = "StatBarsContainer", menuName = "ScriptableObjects/StatBarsContainer")]
    public class StatBarsContainerSO : ScriptableObject {

        [ field: SerializeField, Header( "Health bar prefab" ) ] public PlayerStatBarView HealthBarPrefab {get; private set; }
        
         [ field: SerializeField, Header( "Hunger bar prefab" ) ] public PlayerStatBarView HungerBarPrefab {get; private set; }

         [ field: SerializeField, Header( "Thirst bar prefab" ) ] public PlayerStatBarView ThirstBarPrefab {get; private set; }

    }
}
