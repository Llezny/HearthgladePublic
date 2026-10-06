using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Hearthglade.Gameplay.UI.HUD
{
    public class TooltipManager : HUD {    
        [SerializeField] TextMeshProUGUI toolTipTitle = null;
        [SerializeField] TextMeshProUGUI toolTipContent = null;
        [SerializeField] GameObject tooltipHolder = null;
        [SerializeField] Vector2 toolTipOffset = new Vector2();

        const float TIME_TO_SHOW_TOOLTIP = 0.5f;
        const float COUNTING_STEP = 0.3f;
        float timePressed = 0;
        Coroutine counter;
    

        IEnumerator CountPressTime() {
            var waiter = new WaitForSeconds(COUNTING_STEP);
            var counter = 0f;
            while (counter < TIME_TO_SHOW_TOOLTIP) {
                counter += COUNTING_STEP;
                yield return waiter;
            }
            tooltipHolder.SetActive(true);
            tooltipHolder.transform.DOScale( 1, 0.1f );
        }

        public void SetupTooltipAndStartCounter( string title, string content ) {
            toolTipTitle.text = title;
            toolTipContent.text = content;
            Vector2 pos = Input.mousePosition;
            tooltipHolder.GetComponent<RectTransform>().position = pos + toolTipOffset;
            counter = StartCoroutine( CountPressTime() );
        }

        public void HideToolTip() {
            if(counter is null) {
                return;
            }
            StopCoroutine( counter );
            tooltipHolder.transform.DOScale( 0, 0.1f )
                .onComplete += () => tooltipHolder.SetActive( false );
        }
    }
}
