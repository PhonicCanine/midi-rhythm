using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidiVersion
{
    class SecondAlternateDifficultyManager : IDifficultyManager
    {
        public IGenerator generator { get; set; }
        double PlayerHealth = 0.5;
        public void AddHit(HitResult hr)
        {
            PlayerHealth += hr switch
            {
                HitResult.Miss => -0.03,
                HitResult.Meh => -0.01,
                HitResult.OK => 0,
                HitResult.Great => 0.01,
                HitResult.Perfect => 0.03,
                _ => 0
            };
            PlayerHealth = Math.Max(0, Math.Min(PlayerHealth, 1));
            generator.SetDifficulty(PlayerHealth);
        }

        public double GetDifficulty()
        {
            return PlayerHealth;
        }

        public string GetName()
        {
            return "Alternate";
        }

        public void SetInitialDifficulty(double difficulty)
        {
            PlayerHealth = difficulty;
        }
    }
}
