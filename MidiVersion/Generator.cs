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
            Clockwise = -1,
            Linear,
            Anticlockwise,
            Indeterminate
        }

        public readonly static Vector2 NULL_VECTOR = new Vector2(-2);
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

        public Orientation ReverseOrientation(Orientation o)
        {
            if (o == Orientation.Indeterminate || o == Orientation.Linear) return o;
            return (Orientation)(-(int)o);
        }
        public int GetClosestTempoDivisorFromNoteSpacing(Note n1, Note n2)
        {
            // Assume n2 comes later than n1.
            TimeSpan n1Start = n1.startTime;
            TimeSpan n2Start = n2.startTime;
            TimeSpan diff = n2Start - n1Start;
            double diffSeconds = diff.TotalSeconds;
            int ret = (int)Math.Round(60.0 / (currentTempo * diffSeconds));
            return ret;
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
            overallDifficulty = 0.2;
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

        public Vector2 CreateDistanceVector(double dist, double angle) => new PolarVector2(dist, angle).ToVector2(aspectRatio);
        
        public bool IsPositionWithinPlayfield(Vector2 pos)
        {
            if (pos.X <= 1 && pos.Y <= 1 && pos.X >= -1 && pos.Y >= -1) return true;
            return false;
        }

        public enum PlayfieldSide { Top, Right, Bottom, Left }
        public List<PlayfieldSide> GetCloseEdges(double dist, Vector2 previous)
        {
            Vector2 change1 = CreateDistanceVector(dist, 0);
            Vector2 change2 = CreateDistanceVector(dist, Math.PI/2.0);
            Vector2 change3 = CreateDistanceVector(dist, Math.PI);
            Vector2 change4 = CreateDistanceVector(dist, 3.0 * Math.PI / 2.0);
            List<PlayfieldSide> closeSides = new List<PlayfieldSide>();
            if (!IsPositionWithinPlayfield(previous + change1)) closeSides.Add(PlayfieldSide.Right);
            if (!IsPositionWithinPlayfield(previous + change2)) closeSides.Add(PlayfieldSide.Top);
            if (!IsPositionWithinPlayfield(previous + change3)) closeSides.Add(PlayfieldSide.Left);
            if (!IsPositionWithinPlayfield(previous + change4)) closeSides.Add(PlayfieldSide.Bottom);

            return closeSides;
        }

        public Orientation GetCurrentOrientation()
        {
            if (previousHitObjects.Count < 2) return Orientation.Indeterminate; // Inconclusive.
            if (previousHitObjects.Count == 2)
            {
                return Orientation.Linear;
            }
            // Get previous three hitcircles.
            LinkedListNode<HitObject> ptr = previousHitObjects.Last;
            HitObject h1 = ptr.Value;
            ptr = ptr.Previous;
            HitObject h2 = ptr.Value;
            ptr = ptr.Previous;
            HitObject h3 = ptr.Value;
            ptr = ptr.Previous;
            Orientation o = GetOrientation(h3, h2, h1);
            // Keep going backwards until we find a nonLinear orientation.
            while (o == Orientation.Linear && ptr.Value != null)
            {
                h1 = h2;
                h2 = h3;
                h3 = ptr.Value;
                o = GetOrientation(h3, h2, h1);
                ptr = ptr.Previous;
            }
            return o;
        }

        /// <summary>
        /// Gets the next position within the playfield, assuming there is at least one previous hitcircle.
        /// </summary>
        /// <param name="dist">The required distance between the previous hitcircle and the next one.</param>
        /// <returns>A vector representing the position of the next hitcircle.</returns>
        public Vector2 GetNextPositionWithinPlayField(double dist)
        {
            Random r = new Random();
            if (double.IsInfinity(dist)) return new Vector2((float)(r.NextDouble() * 2.0 - 1), (float)(r.NextDouble() * 2.0 - 1));

            LinkedListNode<HitObject> ptr = previousHitObjects.Last;
            HitObject last = ptr.Value;
            List<PlayfieldSide> closeSides = GetCloseEdges(dist, last.position);
            // Three cases to consider:

            // Case 1: potential distance vector + previous circle position has x and y values less than 1 for sure.
            if (closeSides.Count == 0)
            {
                // We have all the freedom to place the hitcircle wherever we want, as long as orientation is maintained.
                Orientation orientation = GetCurrentOrientation();
                if (orientation == Orientation.Indeterminate)
                {
                    double angle = 2 * Math.PI * r.NextDouble();
                    Vector2 distanceVector = CreateDistanceVector(dist, angle);
                    return last.position + distanceVector;
                }
                // If the given orientation is linear, we choose a random definitive orientation.
                if (orientation == Orientation.Linear)
                {
                    Orientation[] choices = new Orientation[] { Orientation.Clockwise, Orientation.Anticlockwise };
                    int choice = r.Next(choices.Length);
                    orientation = choices[choice];
                }
                else
                {
                    // Randomise change in orientation.
                    double reverseChance = r.NextDouble();
                    orientation = (reverseChance > 0.7) ? ReverseOrientation(orientation) : orientation;
                }
                // From here, we can guarantee that the orientation is either clockwise or anticlockwise.
                // This also implies that there is a second last hitcircle.
                HitObject secondLast = ptr.Previous.Value;
                Vector2 restrictionLine = last.position - secondLast.position;
                double restrictionAngle = new PolarVector2(restrictionLine).Angle;
                double bufferAngle = (2.0 * Math.PI / 3.0) * r.NextDouble();
                double newAngle;

                if (orientation == Orientation.Clockwise) newAngle = restrictionAngle - bufferAngle;
                else newAngle = restrictionAngle + bufferAngle;

                PolarVector2 polarChangeVector = new PolarVector2(dist, newAngle);
                Vector2 changeVector = polarChangeVector.ToVector2(aspectRatio);
                return last.position + changeVector;
            }
            // Case 2: potential distance vector + previous circle position has only one side over.
            else if (closeSides.Count == 1)
            {
                PlayfieldSide side1 = closeSides[0];
                if (side1 == PlayfieldSide.Top)
                {
                    // Get distance between side and last.position
                    double distanceFromSide = Math.Abs(1 - last.position.Y);
                    Orientation orientation = GetCurrentOrientation();
                    double bufferAngle = Math.Acos(distanceFromSide / dist);
                    double newAngle;
                    if (orientation == Orientation.Indeterminate)
                    {
                        double lower = Math.PI / 2.0 + bufferAngle - 2.0 * Math.PI;
                        double upper = Math.PI / 2.0 - bufferAngle;
                        newAngle = RandDoubleRange(lower, upper);
                        PolarVector2 polarDistanceVector = new PolarVector2(dist, newAngle);
                        Vector2 distanceVector = polarDistanceVector.ToVector2(aspectRatio);
                        return last.position + distanceVector;
                    }
                    else if (orientation == Orientation.Linear)
                    {
                        // Choose orientation randomly.
                        Orientation[] orientations = new Orientation[] { Orientation.Anticlockwise, Orientation.Clockwise };
                        int choice = r.Next(0, 2);
                        orientation = orientations[choice];
                    }

                    HitObject secondLast = ptr.Previous.Value;
                    Vector2 restrictionLine = last.position - secondLast.position;
                    PolarVector2 polarRestrictionLine = new PolarVector2(restrictionLine);
                    if (orientation == Orientation.Anticlockwise)
                    {
                        double lower = Math.Max(polarRestrictionLine.Angle, Math.PI / 2.0 + bufferAngle);
                        double upper = polarRestrictionLine.Angle + 2.0 * Math.PI / 3.0;
                        newAngle = RandDoubleRange(lower, upper);
                    }
                    else
                    {
                        double normalised = polarRestrictionLine.Angle > Math.PI ? polarRestrictionLine.Angle - Math.PI * 2.0 : polarRestrictionLine.Angle;
                        double lower = normalised - 2.0 * Math.PI / 3.0;
                        double upper = Math.Min(normalised, Math.PI / 2.0 - bufferAngle);
                        newAngle = RandDoubleRange(lower, upper);
                    }
                    PolarVector2 polarChangeVector = new PolarVector2(dist, newAngle);
                    Vector2 changeVector = polarChangeVector.ToVector2(aspectRatio);
                    return last.position + changeVector;
                }
                if (side1 == PlayfieldSide.Left)
                {
                    double distanceFromSide = Math.Abs(-1 - last.position.X);
                    Orientation orientation = GetCurrentOrientation();
                    double bufferAngle = Math.Acos(distanceFromSide / dist);
                    double newAngle;
                    if (orientation == Orientation.Indeterminate)
                    {
                        double lower = Math.PI + bufferAngle - 2.0 * Math.PI;
                        double upper = Math.PI - bufferAngle;
                        newAngle = RandDoubleRange(lower, upper);
                        PolarVector2 polarDistanceVector = new PolarVector2(dist, newAngle);
                        Vector2 distanceVector = polarDistanceVector.ToVector2(aspectRatio);
                        return last.position + distanceVector;
                    }
                    else if (orientation == Orientation.Linear)
                    {
                        // Choose orientation randomly.
                        Orientation[] orientations = new Orientation[] { Orientation.Anticlockwise, Orientation.Clockwise };
                        int choice = r.Next(0, 2);
                        orientation = orientations[choice];
                    }

                    HitObject secondLast = ptr.Previous.Value;
                    Vector2 restrictionLine = last.position - secondLast.position;
                    PolarVector2 polarRestrictionLine = new PolarVector2(restrictionLine);
                    if (orientation == Orientation.Anticlockwise)
                    {
                        double lower = Math.Max(polarRestrictionLine.Angle, Math.PI + bufferAngle);
                        double upper = polarRestrictionLine.Angle + 2.0 * Math.PI / 3.0;
                        newAngle = RandDoubleRange(lower, upper);
                    }
                    else
                    {
                        //double normalised = polarRestrictionLine.Angle > Math.PI ? polarRestrictionLine.Angle - Math.PI * 2.0 : polarRestrictionLine.Angle;
                        double lower = polarRestrictionLine.Angle - 2.0 * Math.PI / 3.0;
                        double upper = Math.Min(Math.PI - bufferAngle, polarRestrictionLine.Angle);
                        newAngle = RandDoubleRange(lower, upper);
                    }
                    PolarVector2 polarChangeVector = new PolarVector2(dist, newAngle);
                    Vector2 changeVector = polarChangeVector.ToVector2(aspectRatio);
                    return last.position + changeVector;
                }
                if (side1 == PlayfieldSide.Bottom)
                {
                    double distanceFromSide = Math.Abs(-1 - last.position.Y);
                    Orientation orientation = GetCurrentOrientation();
                    double bufferAngle = Math.Acos(distanceFromSide / dist);
                    double newAngle;
                    if (orientation == Orientation.Indeterminate)
                    {
                        double lower = 3.0*Math.PI/2.0 + bufferAngle - 2.0 * Math.PI;
                        double upper = 3.0*Math.PI/2.0 - bufferAngle;
                        newAngle = RandDoubleRange(lower, upper);
                        PolarVector2 polarDistanceVector = new PolarVector2(dist, newAngle);
                        Vector2 distanceVector = polarDistanceVector.ToVector2(aspectRatio);
                        return last.position + distanceVector;
                    }
                    else if (orientation == Orientation.Linear)
                    {
                        // Choose orientation randomly.
                        Orientation[] orientations = new Orientation[] { Orientation.Anticlockwise, Orientation.Clockwise };
                        int choice = r.Next(0, 2);
                        orientation = orientations[choice];
                    }

                    HitObject secondLast = ptr.Previous.Value;
                    Vector2 restrictionLine = last.position - secondLast.position;
                    PolarVector2 polarRestrictionLine = new PolarVector2(restrictionLine);
                    if (orientation == Orientation.Anticlockwise)
                    {
                        double lower = Math.Max(polarRestrictionLine.Angle, 3.0*Math.PI/2.0 + bufferAngle);
                        double upper = polarRestrictionLine.Angle + 2.0 * Math.PI / 3.0;
                        newAngle = RandDoubleRange(lower, upper);
                    }
                    else
                    {
                        //double normalised = polarRestrictionLine.Angle > Math.PI ? polarRestrictionLine.Angle - Math.PI * 2.0 : polarRestrictionLine.Angle;
                        double lower = polarRestrictionLine.Angle - 2.0 * Math.PI / 3.0;
                        double upper = Math.Min(3.0*Math.PI/2.0 - bufferAngle, polarRestrictionLine.Angle);
                        newAngle = RandDoubleRange(lower, upper);
                    }
                    PolarVector2 polarChangeVector = new PolarVector2(dist, newAngle);
                    Vector2 changeVector = polarChangeVector.ToVector2(aspectRatio);
                    return last.position + changeVector;
                }
                if (side1 == PlayfieldSide.Right)
                {
                    double distanceFromSide = Math.Abs(1 - last.position.X);
                    Orientation orientation = GetCurrentOrientation();
                    double bufferAngle = Math.Acos(distanceFromSide / dist);
                    double newAngle;
                    if (orientation == Orientation.Indeterminate)
                    {
                        double lower = bufferAngle;
                        double upper = 2.0*Math.PI - bufferAngle;
                        newAngle = RandDoubleRange(lower, upper);
                        PolarVector2 polarDistanceVector = new PolarVector2(dist, newAngle);
                        Vector2 distanceVector = polarDistanceVector.ToVector2(aspectRatio);
                        return last.position + distanceVector;
                    }
                    else if (orientation == Orientation.Linear)
                    {
                        // Choose orientation randomly.
                        Orientation[] orientations = new Orientation[] { Orientation.Anticlockwise, Orientation.Clockwise };
                        int choice = r.Next(0, 2);
                        orientation = orientations[choice];
                    }

                    HitObject secondLast = ptr.Previous.Value;
                    Vector2 restrictionLine = last.position - secondLast.position;
                    PolarVector2 polarRestrictionLine = new PolarVector2(restrictionLine);
                    if (orientation == Orientation.Anticlockwise)
                    {
                        double lower = Math.Max(polarRestrictionLine.Angle, bufferAngle);
                        double upper = polarRestrictionLine.Angle + 2.0 * Math.PI / 3.0;
                        newAngle = RandDoubleRange(lower, upper);
                    }
                    else
                    {
                        //double normalised = polarRestrictionLine.Angle > Math.PI ? polarRestrictionLine.Angle - Math.PI * 2.0 : polarRestrictionLine.Angle;
                        double lower = polarRestrictionLine.Angle - 2.0 * Math.PI / 3.0;
                        double upper = Math.Min(2.0 * Math.PI - bufferAngle, polarRestrictionLine.Angle);
                        newAngle = RandDoubleRange(lower, upper);
                    }
                    PolarVector2 polarChangeVector = new PolarVector2(dist, newAngle);
                    Vector2 changeVector = polarChangeVector.ToVector2(aspectRatio);
                    return last.position + changeVector;
                }
                return new Vector2(0);
            }
            // Case 3: potential distance vector + previous circle position is two sides over.
            else if (closeSides.Count == 2)
            {
                return new Vector2(0);
            }
            // Otherwise, the playfield is too small.
            else
            {
                return new Vector2(0);
            }
        }

        public double NormailseAngle(double value)
        {
            if (value < 0)
            {
                int mul = (int)Math.Abs(value / (2.0 * Math.PI)) + 1;
                return value + 2.0 * Math.PI * mul;
            }
            else if (value >= 2.0 * Math.PI)
            {
                int mul = (int)(value / (2.0 * Math.PI));
                return value - 2.0 * Math.PI * mul;
            }
            else return value;
        }

        public double RandDoubleRange(double minimum, double maximum)
        {
            Random r = new Random();
            return r.NextDouble() * (maximum - minimum) + minimum;
        }

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
                // Choose a random position on the playfield.
                return new Vector2((float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1));
            }

            // Consider overall difficulty.
            HitObject previousObject = previousHitObjects.Last.Value;
            Note previousNote = previousObject.associatedNote;
            int closestTempoDivisor = GetClosestTempoDivisorFromNoteSpacing(previousNote, noteToPlace);
            
            // If the closestTempoDivisor is too high, we return NULL_VECTOR, essentially skipping the note.
            if (overallDifficulty < 0.25 && closestTempoDivisor > 1) return NULL_VECTOR;
            if (overallDifficulty < 0.4 && closestTempoDivisor > 2) return NULL_VECTOR;
            //if (overallDifficulty >= 0.4) return NULL_VECTOR; // TEMPORARY FOR NOW. WANT TO CONSIDER LOWER DIFFS.

            double playfieldNoteSpacing;

            // For overall difficulty less than 0.4, we want distance between notes on the timeline to be
            // proportional to distance between notes on the playfield.
            // The overall difficulty does not influence this difference
            // but the BPM and timeline distance does. Fast BPM && close distance on timeline ==> closer spacing.
            // higher overall difficulty ==> more complex rhythm choices.
            //if (overallDifficulty < 0.4)
            //{
            playfieldNoteSpacing = BPMToSPB(previousNote.tempo) / (2 * closestTempoDivisor) + 0.3*overallDifficulty;
            //} else
            //{

            //}

            return GetNextPositionWithinPlayField(playfieldNoteSpacing);

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
            // We use the shoelace formula, order is h1, h2, h3
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
            if (previousHitObjects.Count > 15)
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
                Vector2 nextPosition = GetNextPosition(note);
                
                c.position = nextPosition;
                if (nextPosition != NULL_VECTOR) AddHitObject(c);
                yield return c;
            }
        }
    }
}
