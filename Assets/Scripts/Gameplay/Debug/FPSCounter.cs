using System.Collections;
using Hearthglade.Gameplay.UI.HUD;
using UnityEngine;

namespace Hearthglade.Gameplay.Debug
{
	public class FPSCounter : MonoBehaviour {
		private uint current;
		private uint min, max;
		private ulong currentSum;
		private ulong probesCounter;
		private GUIStyle guiStyle = new GUIStyle();
	
		IEnumerator Start () {
			guiStyle.fontSize = 25;
			guiStyle.normal.textColor = Color.white;
			GUI.depth = 2;
			probesCounter = 0; 
			min = 1000;
			max = 0;
			while (true) {
				current = (uint)(1f / Time.unscaledDeltaTime);
				currentSum += current;
				probesCounter++;
				min = (uint)Mathf.Min( min, current );
				max = (uint)Mathf.Max( max, current );
			
				yield return new WaitForSeconds (1f);
			}
		}
	
		void OnGUI() {
			GUI.Label (new Rect (5, 40, 100, 25), "fps: " +  current, guiStyle );
			GUI.Label (new Rect (5, 60, 100, 25), "avg: " + ( currentSum / probesCounter ), guiStyle );
			GUI.Label (new Rect (5, 80, 100, 25), "min: " + min, guiStyle );
			GUI.Label (new Rect (5, 100, 100, 25), "max: " + max, guiStyle );
		}
	}
}