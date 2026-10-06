using UnityEngine;

namespace Hearthglade.Gameplay.Debug
{
    public class ScreenshotMaker : MonoBehaviour
    {
        [SerializeField] int scale = 2;

        //private const string SCREENSHOT_PATH = "C:/Users/Łukasz/Pictures/";

        public void MakeScreenshot(){
            string date = System.DateTime.Now.ToString();
            date = date.Replace("/","-");
            date = date.Replace(" ","_");
            date = date.Replace(":","-");
            var path = $"{Application.dataPath}/screenshot_{date}.png";
            ScreenCapture.CaptureScreenshot(path , scale);
            UnityEngine.Debug.Log("Saved screenshot as " + path);
        }

        private void Update() {
            if(Input.GetKey(KeyCode.Home)){
                MakeScreenshot();
            }
        }

    }
}
