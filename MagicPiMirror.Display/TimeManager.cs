using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SystemOut.MagicPiMirror
{
    internal class TimeManager
    {
        public static TimeSpan CurrentOffset { get; private set; } = new TimeSpan(0, 0, 0);

        public static DateTime Now => DateTime.Now - CurrentOffset;
        public static DateTime Today => DateTime.Today - CurrentOffset;

        internal static void UpdateOffset(DateTime currentCorrectTime)
        {
            CurrentOffset = DateTime.UtcNow - currentCorrectTime;
        }
    }
}
