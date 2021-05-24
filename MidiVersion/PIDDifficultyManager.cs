using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidiVersion
{
    class PIDDifficultyManager : IDifficultyManager
    {
        public IGenerator generator { get; set; }
        double PlayerHealth = 0;
        double LastHealth = 0;
        double Difficulty = 0;
        double Delta;
        double LastDelta;
        double TotalDelta;
        double DeltaRate;
        public void AddHit(HitResult hr)
        {
            LastHealth = PlayerHealth;
            PlayerHealth += hr switch
            {
                HitResult.Miss => -0.5,
                HitResult.Meh => -0.1,
                HitResult.OK => 0,
                HitResult.Great => 0.1,
                HitResult.Perfect => 0.5,
                _ => 0
            };
            LastDelta = Delta;
            Delta = PlayerHealth - LastHealth;
            TotalDelta += Delta;
            DeltaRate = Delta - LastDelta;
            Difficulty = Delta*0.001 + TotalDelta + DeltaRate*0.001;
            TotalDelta = Math.Min(Math.Max(0, TotalDelta),1);
            generator.SetDifficulty(Difficulty);
        }

        public double GetDifficulty()
        {
            return Difficulty;
        }

        public string GetName()
        {
            return "PID";
        }

        public void SetInitialDifficulty(double difficulty)
        {
            TotalDelta = difficulty;
        }
    }
}
