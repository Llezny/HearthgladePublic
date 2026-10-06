using Cysharp.Threading.Tasks;
using Hearthglade.Gameplay.Common.Bootstrap;
using UnityEngine;

namespace Hearthglade.Gameplay.Common {
    public static class Bootstrapper {
        [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.BeforeSceneLoad )]
        public static void Execute( ) {
            AppBootstrapPipelineRunner.RunAsync( ).Forget( );
        }
    }
}