using System;
using System.Collections.Generic;
using System.Linq;

namespace MagicMirror
{
    internal class SpecialDayCalendar
    {
        private readonly List<SpecialDay> specialDays = new List<SpecialDay>
        {

        };

        public IEnumerable<SpecialDay> GetSpecials(DateTime date)
        {
            return specialDays.Where(specialDay => specialDay.Date.DayOfYear == DateTime.Now.DayOfYear);
        }
    }
}