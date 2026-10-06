
namespace Hearthglade.Gameplay.Map
{
    public interface ISaveable {
        object CaptureState();
        void RestoreState( object state );
    }
}