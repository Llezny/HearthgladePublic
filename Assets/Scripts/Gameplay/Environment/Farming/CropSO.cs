using Hearthglade.Core.Farming;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.Resource;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment.Farming
{
    // Authoring data of one crop. The asset name is the crop id; the growth rules are baked into a Core CropDefinition.
    [CreateAssetMenu(fileName = "NewCrop", menuName = "ScriptableObjects/Farming/Crop")]
    public class CropSO : ScriptableObject {
        public CropLifecycle lifecycle;
        public PlotType plotType;

        [Header( "Items" )]
        [Tooltip( "What gets planted (a seed, a sapling, a cutting)." )]
        public ItemSO seed;
        public ItemSO produce;
        [Range( 0f, 1f ), Tooltip( "Chance that one harvest also gives a seed back." )]
        public float seedDropChance = 0.3f;

        [Header( "Growth, in game minutes (a day is 1440, a game minute is about 3.3 real seconds)" )]
        [Tooltip( "How long every growth stage lasts, except the last, mature one." )]
        public int[] stageMinutes = { 60, 60 };
        [Tooltip( "Annual: produce per harvest. Perennial: fruit the plant holds at once." )]
        public int maxYield = 1;
        [Tooltip( "Perennial only: minutes for one more fruit to ripen." )]
        public int fruitRegrowMinutes;

        [Header( "View" )]
        public GameObject visualPrefab;
        [Tooltip( "Gathering animation, sound, distance and the skill that speeds it up." )]
        public ResourceSO harvest;

        [Header( "Animals" )]
        [Tooltip( "What kind of food a ripe crop is to an animal; an animal raids it when its diet includes this." )]
        public ForageKind forageKind = ForageKind.None;

        [Header( "Climate (world scale: temperature -1 cold .. 1 hot, humidity -1 dry .. 1 wet)" )]
        [Tooltip( "Full growth speed inside this range. Outside it the crop grows slower, down to half speed 0.3 away; it never dies." )]
        [Range( -1f, 1f )] public float temperatureFrom = -1f;
        [Range( -1f, 1f )] public float temperatureTo = 1f;
        [Range( -1f, 1f )] public float humidityFrom = -1f;
        [Range( -1f, 1f )] public float humidityTo = 1f;

        public ItemId Id => new ItemId( name );
        public string DisplayName => produce != null ? produce.itemName : name;

        public CropDefinition ToDefinition() {
            return new CropDefinition(
                Id,
                lifecycle,
                plotType,
                stageMinutes ?? new int[ 0 ],
                maxYield,
                fruitRegrowMinutes,
                produce != null ? produce.Id : default,
                seed != null ? seed.Id : default,
                seedDropChance,
                forageKind,
                new ClimateRange( temperatureFrom, temperatureTo, humidityFrom, humidityTo )
            );
        }
    }
}
