using System;
using System.Collections.Generic;
using DG.Tweening;
using Hearthglade.Gameplay.Common.Service;
using Hearthglade.Gameplay.Environment;
using Hearthglade.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class InteractionIcon : MonoBehaviour  {
    
    [ SerializeField ] Image interactionIcon;
    
    private InteractBehaviour interactBehaviour;
    private CanvasService canvasService;
    
    private Camera mainCamera;
    private RectTransform canvasRectTransform;

    private IInteractable targetObject;
    private Transform targetObjectTransform;
    private Vector3 targetObjectCenter = Vector3.zero;
    
    private bool isIconInAnimation = false;
    private Tween hidingTween;

    private static readonly Vector2 ScreenSpaceIconOffset = new Vector2(20f, 20f);

    [ Inject ]
    public void Construct( InteractBehaviour interactBehaviour, CanvasService canvasService ) {
        this.interactBehaviour = interactBehaviour;
        this.canvasService = canvasService;
        this.canvasRectTransform = canvasService.InteractionIconCanvas.GetComponent<RectTransform>();
        interactBehaviour.ObjectSelected += SetNewTarget;
        mainCamera = Camera.main;
    }

    private void OnEnable( ) {
        if ( !interactBehaviour ) {
            return;
        }
        interactBehaviour.ObjectSelected += SetNewTarget;
        interactBehaviour.ObjectUnselected += RemoveOldTarget;
    }

    private void OnDisable( ) {
        if ( !interactBehaviour ) {
            return;
        }
        interactBehaviour.ObjectSelected -= SetNewTarget;
        interactBehaviour.ObjectUnselected -= RemoveOldTarget;
    }

    private void Update( ) {
        if ( !targetObjectTransform ) {
            return;
        }
        SetIconPos();
    }

    private void SetIconPos( ) {
        Vector3 offsetPos = new Vector3(
            targetObjectTransform.position.x,
            targetObjectTransform.position.y + targetObjectCenter.y,
            targetObjectTransform.position.z 
        );
        
        Vector2 posOnScreen = mainCamera.WorldToScreenPoint(offsetPos);
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform,
            posOnScreen, 
            null, 
            out var localPosOnCanvas 
        );
        
        interactionIcon.rectTransform.localPosition = localPosOnCanvas + ScreenSpaceIconOffset;
    }

    private void CacheTargetObjectCenter( ) {
        var colliderComponent = targetObjectTransform.GetComponent<Collider>();
        if (colliderComponent) {
            targetObjectCenter = colliderComponent.bounds.center;
        }
    }

    private void SetNewTarget( IInteractable interactable ) {
        interactionIcon.gameObject.SetActive( true );
        interactionIcon.transform.DOScale(Vector3.one, 0.1f);
        targetObject = interactable;
        targetObjectTransform = interactable.GetTransform();
        CacheTargetObjectCenter( );
        
        if ( isIconInAnimation && hidingTween != null ) {
            hidingTween.Kill( );
            hidingTween = null;
        }
    }

    private void RemoveOldTarget() {
        hidingTween = interactionIcon.transform.DOScale(Vector3.zero, 0.1f);
        isIconInAnimation = true;
        hidingTween.onComplete += () => {
            isIconInAnimation = false;
            targetObject = null;
            targetObjectTransform = null;
            interactionIcon.gameObject.SetActive( false );
        };
    }
    
}
