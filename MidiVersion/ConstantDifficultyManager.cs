using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidiVersion
{
    class ConstantDifficultyManager : IDifficultyManager
    {
        public IGenerator generator { get; set; }

        public void AddHit(HitResult hr)
        {
            generator.SetDifficulty(Difficulty);
        }

        double Difficulty = 0;

        public double GetDifficulty()
        {
            return Difficulty;
        }

        public string GetName()
        {
            return "Constant";
        }

        public void SetInitialDifficulty(double difficulty)
        {
            Difficulty = difficulty;
        }
    }
}
