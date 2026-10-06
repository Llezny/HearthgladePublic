using System;
using UnityEngine;

namespace Hearthglade.Gameplay.Environment
{
    public interface IInteractable {
        string TooltipTitle => "";  
        string TooltipDescription => "Tap to interact";
        float InteractionDistance => 0.4f;
        float InteractionDuration => 0.1f;
        AnimationClip InteractionAnim => null;
        InteractionTiming InteractionTiming => InteractionTiming.Instant;
        InteractionType InteractionType => InteractionType.Other;
        
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
