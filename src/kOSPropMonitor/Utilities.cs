using System.Text;
using System.Collections.Generic;
using System;

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

        public static string FreeFormat(string input, Dictionary<string, object> p)
        {
            foreach (KeyValuePair<string, object> kvpair in p)
                input = ReplaceString(input, kvpair.Key, (kvpair.Value).ToString(), StringComparison.InvariantCultureIgnoreCase);

            return input;
        }

        public static string ReplaceString(string str, string oldValue, string newValue, StringComparison comparison)
        {
            StringBuilder sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, comparison);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }
    }
}
