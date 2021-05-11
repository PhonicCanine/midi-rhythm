using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidiVersion
{   
    public class Note : IComparable
    {
        public int num;
        public TimeSpan startTime;
        public TimeSpan duration;
        public byte velocity; // intensity of the note.
        public byte noteNumber; // Measure of pitch.
        public double tempo;
        public Track fromTrack;

        public int CompareTo(object obj)
        {
            if (obj is Note)
            {
                if (startTime < ((Note)obj).startTime) return -1;
                else if (startTime > ((Note)obj).startTime) return 1;
            }
            return 0;
        }
    }
}
