using System;
using Hearthglade.Gameplay.Resource;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment
{
    public interface IInteractable {
        string TooltipTitle => "";  
        string TooltipDescription => "Tap to interact";
        float InteractionDistance => 0.4f;
        float InteractionDuration => 0.1f;
        // A clip of its own that wins over the gathering motions (fishing, the well); null lets InteractionAnimationPicker choose.
        AnimationClip AnimationOverride => null;
        // What the interaction gathers; null when it is not gathering, which plays no work animation.
        ResourceSO Gathered => null;
        InteractionTiming InteractionTiming => InteractionTiming.Instant;
        InteractionType InteractionType => InteractionType.Other;
        // What the player murmurs when they tap this object although CanInteract is false; null stays silent.
        string RefusalMessage => null;

        Action<IInteractable> InteractableEnabled {get; set;}
        Action<IInteractable> InteractableDisabled {get; set;}
        
        IInteractable GetInteractable();
        Transform GetTransform( );
        void InteractionStart() {}
        void InteractCancelCallback( ) {}
        void InteractionCompleted() {}
        bool CanInteract();
    }
}
