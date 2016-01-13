using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SystemOut.MagicPiMirror
{
    public static class Extensions
    {
        public static string FirstCharToUpper(this string input)
        {
            if (input == null)
                throw new ArgumentException("Please provide a non null string.");
            if (input == string.Empty)
                return input;
            return input.First().ToString().ToUpper() + input.Substring(1);
        }
    }
}
