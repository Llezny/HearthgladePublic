using Hearthglade.Gameplay.Player.Stats;

namespace Hearthglade.Gameplay.Resource
{
    // How long gathering takes: the base time, shortened by the player's skill the resource asks for.
    public static class GatheringTime
    {
        public const float BaseSeconds = 6f;

        public static float Multiplier( ResourceSO resource, PlayerStatsComponent stats ) {
            if( resource == null || resource.StatUsedForGathering == StatsMap.none ) {
                return 0f;
            }
            return stats.GetPlayerStat( resource.StatUsedForGathering ).CurrentValue;
        }

        public static float Seconds( ResourceSO resource, PlayerStatsComponent stats ) {
            return BaseSeconds - BaseSeconds * Multiplier( resource, stats );
        }
    }
}
