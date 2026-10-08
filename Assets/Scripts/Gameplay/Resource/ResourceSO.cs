using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Items;
using UnityEngine;
using UnityEngine.Serialization;

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

        [ Tooltip( "Only for gathering unlike any other (fishing, the well). Leave empty and InteractionAnimationPicker chooses by the skill and the tool in use." ) ]
        [ FormerlySerializedAs( "InteractionAnim" ) ]
        public AnimationClip AnimationOverride;
        public string ToolTipMessage;
        public int NumOfItemsOnGather = 2;
        public bool DestroyObjectOnGather = true;
        public ActionOnResourceGather ActionOnResourceGather;

        [ Header( "Gathering" ) ]
        [ Tooltip( "Which tool stat speeds the work up (axe = Chop, pickaxe = Mine, sickle = Harvest). None: no tool ever helps, it is picked up by hand." ) ]
        public GatherSkill Skill = GatherSkill.Harvest;
        [ Tooltip( "Cannot be done without a tool that has the skill; the player is told which one is missing. Otherwise bare hands work and a tool only speeds it up." ) ]
        public bool RequiresTool;
        [ Tooltip( "Seconds with bare hands (speed 1); a tool divides it by its speed." ), Min( 0.1f ) ]
        public float BaseSeconds = 6f;


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