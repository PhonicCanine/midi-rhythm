using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidiVersion
{
    public enum HitResult : int
    {
        Perfect = 315,
        Great = 300,
        OK = 100,
        Meh = 50,
        Miss = 0
    }
}
