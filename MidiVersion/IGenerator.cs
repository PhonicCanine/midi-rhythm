using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MidiVersion
{
    public interface IGenerator
    {
        public IEnumerable<HitObject> GetHitObjects();
        public void Initialize(Grid playfield, MainWindow game);
        public void SetDifficulty(double difficulty);
    }
}
