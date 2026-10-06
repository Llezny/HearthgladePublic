using System.Collections.Generic;
using DG.Tweening;
using Hearthglade.Gameplay.Debug;
using Hearthglade.Gameplay.Helpers;
using Hearthglade.Gameplay.Items;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Hearthglade.Gameplay.UI.Menu.Fishing
{
    public class FishingPopup : Common.Menu {

        private const float MIN_SCALE = 0.5f;
        private const float MAX_SCALE = 0.8f;
        private const float WIN_SCALE_THRESHOLD = 0.6f;
        private const float ANIM_DURATION = 1.5f;

        private readonly Color activeButtonColor = new Color(  0.49f, 0.62f, 0.54f, 0.59f );
        private readonly Color unactiveButtonColor = new Color(  0.82f, 0.39f, 0.42f, 0.59f );

        [ SerializeField ] RectTransform fishingRingTransform;
        [ SerializeField ] Image fishingButtonIcon;
        [ SerializeField ] List<ItemSO> possibleRewards;
        [ SerializeField ] Slider progressBarSlider;
        Image fishingRingImage;

        private float currentScale;
        private float currentProgress;
        private Tween currentTween;
        private bool isButtonPressed;
        private RandomNumberGenerator<float> randomScaleGenerator;
        private FishHole targetFishHole;
        private InventoryService inventoryService;
        
        [ Inject ]
        public void Construct( InventoryService inventoryService ) {
            this.inventoryService = inventoryService;
        }
        private void Awake() {
            fishingRingImage = fishingRingTransform.GetComponent<Image>();
            randomScaleGenerator = new RandomNumberGenerator<float>();
            randomScaleGenerator.Add(2, 0.5f);
            randomScaleGenerator.Add(1, 0.6f);
            randomScaleGenerator.Add(1, 0.7f);
            randomScaleGenerator.Add(1, 0.8f);
        }

        public void OpenMenu( FishHole fishHole ) {
            if( fishHole is null ) {
                UnityEngine.Debug.LogError( "Cannot open fishing menu when Fishhole is null" );
                return;
            }
            targetFishHole = fishHole;
            currentProgress = 0;
            currentScale = MAX_SCALE;
            progressBarSlider.value = 0;
            progressBarSlider.maxValue = 3;
            progressBarSlider.minValue = 0;
            base.OpenMenu();
            this.SetNewScale();
        }

        public override void CloseMenu(bool fadeOutShroud = false) {
            base.CloseMenu(fadeOutShroud);
            targetFishHole = null;
            DestroyTween();
            SetButtonUnpressed();
        }

        public void SetButtonPressed() {
            isButtonPressed = true;
        }

        public void SetButtonUnpressed() {
            isButtonPressed = false;
        }

        private void UpdateMinigame() {
            UpdateRing();
            UpdateFishingProgress();
        }

        private void UpdateFishingProgress() {
            progressBarSlider.value = currentProgress;
            if(!isButtonPressed){
                currentProgress = Mathf.Max(currentProgress - ( Time.deltaTime / 20 ), 0);
                return;
            }

            if(fishingRingImage.color == activeButtonColor) {
                currentProgress = Mathf.Min(currentProgress + Time.deltaTime, 3);
                if(currentProgress >= 3) {
                    targetFishHole?.Destroy();
                    CloseMenu();
                    AddRewardToPlayer();
                }
            }
            else {
                currentProgress = Mathf.Max(currentProgress - Time.deltaTime, 0);
            }
        }

        private void SetNewScale() {
            var newRand = randomScaleGenerator.NextItem();
            currentTween = fishingRingTransform.DOScale( currentScale, ANIM_DURATION ).SetEase(Ease.InOutSine);
            currentTween.onComplete += SetNewScale;
            currentTween.onUpdate += UpdateMinigame;
            currentScale = newRand;
        }

        private void DestroyTween() {
            currentTween.Kill();
            currentTween = null;
        }

        private void UpdateRing() {
            if( fishingRingTransform.localScale.x < WIN_SCALE_THRESHOLD ) {
                fishingRingImage.color = activeButtonColor;
                fishingButtonIcon.color = activeButtonColor;
            }
            else {
                fishingRingImage.color = unactiveButtonColor;
                fishingButtonIcon.color = unactiveButtonColor;
            }
        }
        private void AddRewardToPlayer() {
            inventoryService.AddItem( possibleRewards[0] );
        }

        [ExecuteFromConsole("gofish", "Open fishing popup")]
        public static string OpenMenuCommand( string input = ""){
            FishingPopup obj = GameObject.FindObjectOfType<FishingPopup>();
            if(obj is null) {
                return "not found fishing popup";
            }
            obj.OpenMenu();
            return "Open fishing popup";
        }
    }
}
