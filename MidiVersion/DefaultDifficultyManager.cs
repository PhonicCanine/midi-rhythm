using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidiVersion
{
    class DefaultDifficultyManager : IDifficultyManager
    {
        public IGenerator generator { get; set; }

        public double SigmoidDiffChange(double x)
        {
            return (1.0 / 30.0) * (12.0 * Math.Exp(-12.0 * (x - 0.5))) / Math.Pow(1.0 + Math.Exp(-12.0 * (x - 0.5)), 2);
        }
        double DifficultyRadius = 0.7;
        public void AddHit(HitResult hr)
        {
            if (hr == HitResult.Great) DifficultyRadius += SigmoidDiffChange(DifficultyRadius);
            else if (hr == HitResult.Miss) DifficultyRadius -= SigmoidDiffChange(DifficultyRadius);
            generator.SetDifficulty(DifficultyRadius);
        }
        public double GetDifficulty()
        {
            return DifficultyRadius;
        }
    }
}
