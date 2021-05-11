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
        Vector2 NULL_VECTOR = new Vector2(-2);
        const double MAX_OVERALL_DIFFICULTY = 0.4;
        const double MIN_OVERALL_DIFFICULTY = 0;
        double playfieldLength;
        double playfieldHeight;
        double aspectRatio;
        double currentTempo; // in BPM. Assume a 4/4 time signature.
        private double difficultyRadius; // Radius of the large circle to place notes on
        List<Note> mergedLandmarks; // All the instruments merged into the same list. Duplicate notes have an averaged "pitch"
        // Helps determine how far each note should be placed. When below 0.5, the circles should not overlap. Max is 1
        // For simplicity we don't factor in harder diffs.
        double overallDifficulty;
        LinkedList<HitObject> previousHitObjects;
        MainWindow game;
        int numObjectsHit = 0;
        Grid playfield;

        public double OverallDifficulty
        {
            get
            {
                return overallDifficulty;
            }
            set
            {
                if (value < MIN_OVERALL_DIFFICULTY) overallDifficulty = MIN_OVERALL_DIFFICULTY;
                else if (value > MAX_OVERALL_DIFFICULTY) overallDifficulty = MAX_OVERALL_DIFFICULTY;
                else overallDifficulty = value;
            }
        }

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
        public int GetClosestTempoDivisorFromNoteSpacing(Note n1, Note n2)
        {
            // Assume n2 comes later than n1.
            TimeSpan n1Start = n1.startTime;
            TimeSpan n2Start = n2.startTime;
            TimeSpan diff = n2Start - n1Start;
            double diffSeconds = diff.TotalSeconds;

            return (int)Math.Round(60.0 / (currentTempo * diffSeconds));
        }

        public Track MergeTracks(List<Track> tracks)
        {
            Track newTrack = new Track();
            newTrack.name = "All Notes";
            newTrack.notes = new List<Note>();
            foreach (Track track in tracks)
            {
                newTrack.notes.AddRange(track.notes);
            }
            newTrack.notes.Sort(); // Sort the notes in chronologial order.
            return newTrack;
        }

        
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

        /// <summary>
        /// The old NextPosition function.
        /// </summary>
        /// <returns>A PolarVector2 representing the next hitcircle position.</returns>
        public PolarVector2 GetNextPolarPosition()
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

        /// <summary>
        /// Converts BPM to SPB (Seconds per Beat)
        /// </summary>
        /// <returns></returns>
        public double BPMToSPB(double bpm) => 60.0 / bpm;

        /// <summary>
        /// This is the NextPosition function that we want to use.
        /// </summary>
        /// <param name="noteToPlace">The note that will be associated with the placed hitcircle.</param>
        /// <returns>A vector containing the next position.</returns>
        public Vector2 GetNextPosition(Note noteToPlace)
        {
            Random r = new Random();
            if (previousHitObjects.Count == 0)
            {
                return new Vector2((float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1));
            }

            // Consider overall difficulty.
            HitObject previousObject = previousHitObjects.Last.Value;
            Note previousNote = previousObject.associatedNote;
            int closestTempoDivisor = GetClosestTempoDivisorFromNoteSpacing(previousNote, noteToPlace);
            
            // If the closestTempoDivisor is too high, we return NULL_VECTOR, essentially skipping the note.
            if (overallDifficulty < 0.25 && closestTempoDivisor > 1) return NULL_VECTOR;
            if (overallDifficulty < 0.4 && closestTempoDivisor > 2) return NULL_VECTOR;
            if (overallDifficulty >= 0.4) return NULL_VECTOR; // TEMPORARY FOR NOW. WANT TO CONSIDER LOWER DIFFS.

            double playfieldNoteSpacing;

            // For overall difficulty less than 0.4, we want distance between notes on the timeline to be
            // proportionate to distance between notes on the playfield.
            // The overall difficulty does not influence this difference
            // but the BPM and timeline distance does. Fast BPM && close distance on timeline ==> closer spacing.
            if (overallDifficulty < 0.4)
            {
                playfieldNoteSpacing = BPMToSPB(previousNote.tempo) / (2 * closestTempoDivisor);
            } else
            {

            }

            if (previousHitObjects.Count == 1)
            {
                // Choose range of values for the output vector such that note is still on screen 

            } else if (previousHitObjects.Count == 2)
            {
                // Same as above condition, but now the vector to output must not overlap with the 2nd last
                // hitcircle.
            } else
            {
                // Same as above condition, but now the new circle must maintain the orientation defined by the last three circles
                // (as defined by the shoelace formula - the GetOrientation function) unless the circle to place is forced to be off-screen.
            }
            return new Vector2(0);
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

        public Track RemoveDuplicateNotes(Track track)
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

        /// <summary>
        /// Function that generates the dynamic hitcircles one a a time.
        /// </summary>
        /// <returns>A hitobject.</returns>
        public IEnumerable<HitObject> GetHitObjects()
        {
            Track t = RemoveDuplicateNotes(MergeTracks(game.landmarks));

            List<Note> n = t.notes;
            double time = 0;
            foreach (Note note in n)
            {
                currentTempo = note.tempo;
                Circle c = new Circle(game, note) { start = note.startTime };
                //c.SetPolarPosition(GetNextPosition(note), aspectRatio);
                Vector2 nextPosition = GetNextPosition(note);
                if (nextPosition == NULL_VECTOR) continue; // Skip this note.
                c.position = nextPosition;
                AddHitObject(c);
                yield return c;
            }
        }
    }
}
