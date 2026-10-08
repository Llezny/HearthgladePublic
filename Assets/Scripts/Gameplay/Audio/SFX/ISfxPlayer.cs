namespace Hearthglade.Gameplay.Audio {

    /// <summary>Plays sound effects. Lives for the whole life of the app (see AppScope).</summary>
    public interface ISfxPlayer {

        /// <summary>Volume of all sound effects, 0..1. Applies to the sounds played from now on.</summary>
        float Volume { get; set; }

        /// <summary>Plays the sound with its own volume and pitch. A null sound, or one without a clip, is ignored.</summary>
        void Play( AudioSO sfx );
    }
}
