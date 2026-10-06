namespace Hearthglade.Gameplay.Map
{
    public interface ISceneObjectWithData {

        object CaptureState( );
        void RestoreState( object state );
        SceneObjectModel SceneObjectModel { get; set; }

    }
}