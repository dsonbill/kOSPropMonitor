using System.Collections.Generic;

namespace kOSPropMonitor
{
    public static class Utilities
    {
        public static string Format(string input, Dictionary<string, string> p)
        {
            foreach (KeyValuePair<string, string> kvpair in p)
                input = input.Replace("{" + kvpair.Key + "}", (kvpair.Value).ToString());

            return input;
        }
    }
}
