using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Player.Stats;
using UnityEngine;

namespace Hearthglade.Gameplay.Resource
{
    [CreateAssetMenu(fileName = "NewResource", menuName = "ScriptableObjects/Map/Resource/Resource")]
    public class ResourceSO : ScriptableObject {
        private const float BASE_GATHERING_SPEED = 6f;
        public ItemSO ItemSoOnGather;

        [ Header( "Bonus drop" ) ]
        [ Tooltip( "Sometimes dropped on top of the main item, e.g. a seed from a wild plant." ) ]
        public ItemSO BonusItem;
        [ Range( 0f, 1f ) ]
        public float BonusChance;

        public AnimationClip InteractionAnim;
        public string ToolTipMessage;
        public int NumOfItemsOnGather = 2;
        public bool DestroyObjectOnGather = true;
        public ActionOnResourceGather ActionOnResourceGather;
        public StatsMap StatUsedForGathering = StatsMap.none;
        public ItemSO RequiredItem;
        
        [Range(0.4f, 1 )] 
        public float MinInteractionDistance = 0.4f;
        
        [Header( "SFX" )]
        public AudioSO OnPickup;
        public AudioSO OnGather;

        [Header("Replenish config")]
        public float TimeToReplenishInSeconds = 5;
        public float MaxResourcesCount = 2;
        



    }
}