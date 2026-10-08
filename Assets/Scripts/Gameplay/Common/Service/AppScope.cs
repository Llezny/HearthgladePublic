using Hearthglade.Gameplay.Audio;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Common.Service {

    /// <summary>
    /// The root of dependency injection: the one scope that lives for the whole life of the app, from the main
    /// menu on. It is the Root Lifetime Scope of VContainerSettings, so every other scope (GameplayScope) is its
    /// child and can inject what is registered here. Services that must outlive a scene belong here.
    /// </summary>
    public class AppScope : LifetimeScope {

        [ SerializeField ] AudioConfigSO audioConfig;

        protected override void Configure( IContainerBuilder builder ) {
            builder.RegisterInstance( audioConfig );
            builder.RegisterComponentOnNewGameObject<MusicPlayer>( Lifetime.Singleton, "MusicPlayer" ).UnderTransform( transform ).As<IMusicPlayer>( );
            builder.RegisterComponentOnNewGameObject<SfxPlayer>( Lifetime.Singleton, "SfxPlayer" ).UnderTransform( transform ).As<ISfxPlayer>( );
            builder.RegisterEntryPoint<MenuMusicStarter>( Lifetime.Singleton );
        }
    }
}
