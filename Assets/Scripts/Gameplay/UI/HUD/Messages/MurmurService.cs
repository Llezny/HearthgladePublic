using Hearthglade.Gameplay.Player.Controller;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.HUD.Messages
{
    /// <summary>
    /// Murmurs: short thoughts the player voices in a speech bubble over their head - refusals, hints, tooltips.
    /// <code>murmurService.Show( "The door is locked", "Requires: Rusty Brass Key" );</code>
    /// Only one bubble exists; showing another murmur replaces the one on screen.
    /// </summary>
    public sealed class MurmurService
    {
        private const float MIN_DURATION = 2f;
        private const float MAX_DURATION = 6f;
        private const float BASE_DURATION = 1.6f;
        private const float DURATION_PER_CHARACTER = 0.045f;

        private readonly IMessageFactory messageFactory;
        private readonly PlayerController playerController;
        private MurmurBubble bubble;

        public MurmurService( IMessageFactory messageFactory, PlayerController playerController )
        {
            this.messageFactory = messageFactory;
            this.playerController = playerController;
        }

        /// <param name="title">The main line.</param>
        /// <param name="subtitle">An optional smaller line under it.</param>
        /// <param name="duration">Seconds on screen; by default it grows with the length of the text so it can be read.</param>
        public void Show( string title, string subtitle = null, float? duration = null )
        {
            if( string.IsNullOrEmpty( title ) )
            {
                return;
            }
            // The bubble is a scene object: it is gone when the scene is reloaded, so it is made again on demand.
            if( bubble == null )
            {
                bubble = messageFactory.SpawnMurmurBubble();
                if( bubble == null )
                {
                    return;
                }
            }
            bubble.Show( title, subtitle, duration ?? ReadingTime( title, subtitle ), playerController.transform, Camera.main );
        }

        public void Hide()
        {
            if( bubble != null )
            {
                bubble.Hide();
            }
        }

        public static float ReadingTime( string title, string subtitle )
        {
            int length = ( title?.Length ?? 0 ) + ( subtitle?.Length ?? 0 );
            return Mathf.Clamp( BASE_DURATION + DURATION_PER_CHARACTER * length, MIN_DURATION, MAX_DURATION );
        }
    }
}
