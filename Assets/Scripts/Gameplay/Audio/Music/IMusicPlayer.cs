namespace Hearthglade.Gameplay.Audio {

    /// <summary>Plays the music of the game. Lives for the whole life of the app (see AppScope).</summary>
    public interface IMusicPlayer {

        /// <summary>The playlist that is playing (or waiting out a gap between its songs); null when there is none.</summary>
        MusicPlaylistSO CurrentPlaylist { get; }

        /// <summary>Volume of all music, 0..1. Applies at once, also to the song that is already playing.</summary>
        float Volume { get; set; }

        /// <summary>
        /// Starts a playlist; the one that played before fades out. Asking for the playlist that already plays
        /// changes nothing, and null (a map without music) leaves the current playlist playing.
        /// </summary>
        void Play( MusicPlaylistSO playlist );

        /// <summary>Fades the music out and stops it. A negative time uses the config's playlist switch fade.</summary>
        void Stop( float fadeSeconds = -1f );
    }
}
