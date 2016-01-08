using System;
using System.Collections.Generic;
using System.Linq;

namespace SystemOut.MagicPiMirror
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

    internal class SpecialDay
    {
        public string DisplayText
        {
            // TODO: Add localization support
            get
            {
                switch (SpecialDayType)
                {
                    case SpecialDayType.Birthday:
                        return $"I dag er det {GetNameWithEnding()} {(DateTime.Now.Year - Date.Year)} års fødselsdag!";
                    case SpecialDayType.Romantic:
                        return $"<3 {DateTime.Now.Year - Date.Year} års dag <3";
                    default: return "Spændende dag i dag!";
                }
            }
        }

        private string GetNameWithEnding()
        {
            if (string.IsNullOrEmpty(name))
                return name;
            if (name.EndsWith("s"))
                return name + "'";
            return name + "s";
        }

        private readonly string name;

        public DateTime Date { get; set; }
        public SpecialDayType SpecialDayType { get; set; }

        public SpecialDay(DateTime date, SpecialDayType type, string name)
        {
            Date = date;
            SpecialDayType = type;
            this.name = name;
        }
    }

    internal enum SpecialDayType
    {
        Birthday,
        Romantic
    }
}
