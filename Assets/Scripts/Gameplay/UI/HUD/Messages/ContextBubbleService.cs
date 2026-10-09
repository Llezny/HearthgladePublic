using System.Collections.Generic;
using Hearthglade.Gameplay.Environment;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.HUD.Messages
{
    /// <summary>
    /// The menu of actions of an object (<see cref="IContextActions"/>): the speech bubble of the murmurs, floating over the object, with a
    /// tappable line for every action. It has a bubble of its own, so a murmur does not take it away.
    /// <code>contextBubbleService.Show( "Table", actions, tableTransform );</code>
    /// </summary>
    public sealed class ContextBubbleService
    {
        private readonly IMessageFactory messageFactory;
        private MurmurBubble bubble;

        public ContextBubbleService( IMessageFactory messageFactory )
        {
            this.messageFactory = messageFactory;
        }

        public bool IsShown => bubble != null && bubble.HasOptions;

        public void Show( string title, IReadOnlyList<ContextAction> actions, Transform target )
        {
            if( actions == null || actions.Count == 0 || target == null )
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
            bubble.ShowOptions( title, actions, target, Camera.main );
        }

        public void Hide()
        {
            if( bubble != null )
            {
                bubble.Hide();
            }
        }
    }
}
