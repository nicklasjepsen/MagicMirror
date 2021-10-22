using System;

namespace MagicMirror
{
    public class Appointment
    {
        public string Subject { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Duration { get; set; }
        public bool IsPrivate { get; set; }
    }
}
