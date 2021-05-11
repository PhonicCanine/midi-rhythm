using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MidiVersion
{
    public class Generator
    {
        public enum Orientation
        {
            Clockwise,
            Linear,
            Anticlockwise
        }
        double playfieldLength;
        double playfieldHeight;
        double aspectRatio;
        double currentTempo; // in BPM. Assume a 4/4 time signature.
        private double difficultyRadius; // Radius of the large circle to place notes on
        public double DifficultyRadius
        {
            get { return difficultyRadius; }
            set
            {
                if (value > 0.9) difficultyRadius = 0.9;
                else if (value < 0.2) difficultyRadius = 0.3;
                else difficultyRadius = value;
            }
        }
        /// <summary>
        /// Given the current BPM, and the number of equal divisions of the time between two cycles,
        /// output the amount of time in seconds that one of these divisions takes.
        /// </summary>
        /// <param name="divisor">The number of divisions to make.</param>
        /// <returns>The amount of time in one division.</returns>
        public double GetCycleSpacingDivision(double divisor)
        {
            return 60.0 / (currentTempo * divisor);
        }
        public double GetClosestTempoDivisionFromNoteSpacing(Note n1, Note n2)
        {
            // Assume n2 comes later than n1.
            TimeSpan n1Start = n1.startTime;
            TimeSpan n2Start = n2.startTime;
            TimeSpan diff = n2Start - n1Start;
            double diffSeconds = diff.TotalSeconds;

            return Math.Round(60.0 / (currentTempo * diffSeconds));
        }

        // Helps determine how far each note should be placed. When below 0.5, the circles should not overlap. Max is 1
        // For simplicity we don't factor in harder diffs.
        double overallDifficulty;
        public double OverallDifficulty
        {
            get
            {
                return overallDifficulty;
            }
            set
            {
                if (value < 0) overallDifficulty = 0;
                else if (value > 1) overallDifficulty = 1;
                else overallDifficulty = value;
            }
        }
        LinkedList<HitObject> previousHitObjects;
        MainWindow game;

        int numObjectsHit = 0;
        Grid playfield;
        public Generator(Grid playfield, MainWindow game)
        {
            this.playfield = playfield;
            playfieldLength = playfield.ActualWidth;
            playfieldHeight = playfield.ActualHeight;
            DifficultyRadius = 0.7;
            overallDifficulty = 0.5;
            aspectRatio = playfieldLength / playfieldHeight;
            previousHitObjects = new LinkedList<HitObject>();
            this.game = game;
        }

        public double GetNewLength(double previousRadius)
        {
            // New length must satisfy the triangle inequality. DifficultyRadius + previousRadius > GetNewLength()
            double proposed = overallDifficulty / (5.0 * 2.0 * DifficultyRadius);
            if (proposed >= DifficultyRadius + previousRadius) throw new Exception("proposed radius does not satisfy triangle inequality.");
            if (proposed < 0) return 0.3;
            return proposed;
        }

        public PolarVector2 getNextPosition()
        {
            Random r = new Random();
            if (previousHitObjects.Count == 0)
            {
                double theta = r.NextDouble() * 2.0 * Math.PI;
                return new PolarVector2(DifficultyRadius, theta); // Note that theta will always be positive.
            }
            //if (previousHitObjects.Count <= 2)
            //{

            // get previous radius
            HitObject previous = previousHitObjects.Last.Value;
            double previousRadius = previous.PolarPosition.magnitude;
            double previousAngle = previous.PolarPosition.Angle; // radians. Guaranteed to be positive.

            // Get new length;
            double newLength = GetNewLength(previousRadius);

            double val = (Math.Pow(newLength, 2.0) - Math.Pow(previousRadius, 2.0) - Math.Pow(DifficultyRadius, 2.0)) / (-2.0 * previousRadius * DifficultyRadius);
            double angleDifference = Math.Acos(val);
            if (angleDifference.Equals(double.NaN)) throw new Exception("Angle is out of bounds!");
            double[] multipliers = new double[] { -1, 1 };
            int idx = r.Next(2);
            double newAngle = previousAngle + multipliers[idx] * angleDifference;
            return new PolarVector2(DifficultyRadius, newAngle);
            //}
            // Get the previous 2 hitcircles, determine orientation, and place the circle accordingly.
            //HitObject h1 = previousHitObjects.Last.Previous.Value;
            //HitObject h2 = previousHitObjects.Last.Value;
            //double h1Angle = h1.PolarPosition.Angle;
            //double h2Angle = h2.PolarPosition.Angle;
            // Attempt to use a line integral to determine orientation of the two points relative to the current circle.
            // There's 3 hitobjects in the list.

        }

        public double SigmoidDiffChange(double x)
        {
            return (1.0 / 30.0) * (12.0 * Math.Exp(-12.0 * (x - 0.5))) / Math.Pow(1.0 + Math.Exp(-12.0 * (x - 0.5)), 2);
        }
        public void ProcessHitResult(HitResult hr)
        {
            if (hr == HitResult.Great) DifficultyRadius += SigmoidDiffChange(DifficultyRadius);
            else if (hr == HitResult.Miss) DifficultyRadius -= SigmoidDiffChange(DifficultyRadius);
        }

        public Orientation GetOrientation(HitObject h1, HitObject h2, HitObject h3)
        {
            // We use the shoelace formula.
            Vector2 h1Pos = h1.position;
            Vector2 h2Pos = h2.position;
            Vector2 h3Pos = h3.position;
            h2Pos -= h1Pos;
            h3Pos -= h1Pos;
            double determinant = h2Pos.X * h3Pos.Y - h3Pos.X * h2Pos.Y;
            if (determinant < 0) return Orientation.Clockwise;
            if (determinant == 0) return Orientation.Linear;
            return Orientation.Anticlockwise;
        }

        Track RemoveDuplicateNotes(Track track)
        {
            Track newTrack = new Track();
            newTrack.notes = new List<Note>();
            newTrack.name = track.name;
            TimeSpan previousTimeSpan = new TimeSpan(-1);
            foreach (Note note in track.notes)
            {
                if (note.startTime == previousTimeSpan) continue;
                newTrack.notes.Add(note);
                previousTimeSpan = note.startTime;
            }
            return newTrack;
        }

        public void AddHitObject(HitObject h)
        {
            previousHitObjects.AddLast(new LinkedListNode<HitObject>(h));
            if (previousHitObjects.Count > 3)
            {
                previousHitObjects.RemoveFirst();
            }
        }
        public IEnumerable<HitObject> GetHitObjects()
        {
            Track t = RemoveDuplicateNotes(game.landmarks[0]);
            List<Note> n = t.notes;
            double time = 0;
            foreach (Note note in n)
            {
                currentTempo = note.tempo;
                Circle c = new Circle(game, note) { start = note.startTime };
                c.SetPolarPosition(getNextPosition(), aspectRatio);
                AddHitObject(c);
                yield return c;
            }
        }
    }
}
