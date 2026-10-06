using System.Linq;

namespace Hearthglade.Gameplay.Helpers
{
    public abstract class StringHelper {

        public static string RemoveNonDigitCharacters(string input) => new string(input.Where(c => char.IsDigit(c)).ToArray());
        public static string CurrentDateWithTime => System.DateTime.Now.ToString( "dd-MM-yyyy" ) + "-" + System.DateTime.Now.ToString( "hh-mm" );

    }
}