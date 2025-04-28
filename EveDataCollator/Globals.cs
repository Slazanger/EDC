using System.Reflection.Metadata;

namespace EveDataCollator
{
    public class Globals
    {
        public static Dictionary<int, string> NameIDDictionary { get; set; }

        static Globals()
        {
            NameIDDictionary = new Dictionary<int, string>();
        }
    }
}