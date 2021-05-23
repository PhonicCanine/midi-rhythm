using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidiVersion
{
    public interface IDifficultyManager
    {
        public void AddHit(HitResult hr);
        public IGenerator generator { get; set; }
        public double GetDifficulty();
    }
}
