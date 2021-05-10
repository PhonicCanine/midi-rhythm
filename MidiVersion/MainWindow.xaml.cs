using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using Melanchall.DryWetMidi.Interaction;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LengthConverter = Melanchall.DryWetMidi.Interaction.LengthConverter;

namespace MidiVersion
{
    public enum HitResult: int
    {
        Perfect = 315,
        Great = 300,
        OK = 100,
        Meh = 50,
        Miss = 0
    }

    public class PolarVector2
    {
        public double magnitude;
        private double angle; // needs to always be positive for easy calculations.
        public double Angle
        {
            get { return angle; }
            set
            {
                // Need to ensure that angle > 0 when setting.
                if (value < 0)
                {
                    int mul = (int)Math.Abs(value / (2.0 * Math.PI)) + 1;
                    angle = value + 2.0 * Math.PI * mul;
                }
                else if (value >= 2.0*Math.PI)
                {
                    int mul = (int)(value / (2.0 * Math.PI));
                    angle = value - 2.0 * Math.PI * mul;
                }
                else angle = value;
            }
        }

        public PolarVector2(double m, double a)
        {
            magnitude = m;
            angle = a;
        }

        public Vector2 ToVector2()
        {
            return new Vector2((float)(magnitude * Math.Cos(angle)), (float)(magnitude * Math.Sin(angle)));
        }

        public Vector2 ToVector2(double xAspectRatio)
        {
            Vector2 v = ToVector2();
            v.X /= (float)xAspectRatio;
            return v;
        }
    }

    public class HitObject
    {
        public IAcceptsScoreUpdates game;
        public TimeSpan start;
        protected bool interacted = false;
        public int order;
        public HitObject(IAcceptsScoreUpdates attachedTo)
        {
            game = attachedTo;
        }
        public virtual HitResult EvaluateHit(TimeSpan hitTime) => Math.Abs(hitTime.TotalSeconds - start.TotalSeconds) switch
        {
            0 => HitResult.Perfect,
            <= 0.1 => HitResult.Great,
            <= 0.15 => HitResult.OK,
            <= 0.4 => HitResult.Meh,
            _ => HitResult.Miss
        };
        /// <summary>
        /// x = 1 -> rightmost, x = -1 -> leftmost
        /// y = 1 -> topmost, y = -1 -> bottommost
        /// </summary>
        public Vector2 position;
        private PolarVector2 polarPosition;

        public void SetPolarPosition(PolarVector2 p, double aspectRatio)
        {
            polarPosition = p;
            position = p.ToVector2(aspectRatio);
        }

        public PolarVector2 PolarPosition { get { return polarPosition; } }

        public virtual bool Render(Grid view, TimeSpan forTime)
        {
            return false;
        }
        public virtual bool CanDispose(TimeSpan atTime) => atTime > start + TimeSpan.FromSeconds(0.4) || interacted;
        protected static Vector2 GetLocationRelative(Grid view, Vector2 rel) => new Vector2((float)(view.ActualWidth * ((rel.X / 2) + 0.5)), (float)(view.ActualHeight * ((rel.Y / 2) + 0.5)));
        public virtual void DisposeElements(Grid g)
        {

        }
    }

    public class Circle: HitObject
    {
        const float diameter = 75; // Diameter of hitcircle.
        public static Style buttonStyle;
        Button b;
        Ellipse e;
        TimeSpan mostRecent = TimeSpan.Zero;
        public Circle(IAcceptsScoreUpdates game): base(game)
        {

        }
        public override bool Render(Grid view, TimeSpan forTime)
        {
            base.Render(view, forTime);
            double t = Math.Max(0,(start - forTime).TotalSeconds);
            if (t > 0.5)
            {
                return false;
            }
            float left = GetLocationRelative(view, position).X - diameter / 2;
            float top = (float)view.ActualHeight - (GetLocationRelative(view, position).Y + diameter / 2);

            float approachDiameter = (float)(diameter * (t*4+1));
            float approachLeft = GetLocationRelative(view, position).X - approachDiameter / 2;
            float approachTop = (float)view.ActualHeight - (GetLocationRelative(view, position).Y + approachDiameter / 2);


            if (b is null)
            {
                b = new Button { 
                    Style = buttonStyle, 
                    Background = new SolidColorBrush(Colors.Turquoise), 
                    Height = diameter, 
                    Width = diameter, 
                    Margin = new Thickness(left, top, 0, 0), 
                    HorizontalAlignment = HorizontalAlignment.Left, 
                    VerticalAlignment = VerticalAlignment.Top,
                    FontSize = 30
                };
                b.Click += Clicked;
                view.Children.Add(b);
            }
            if (e is null)
            {
                // Add approach circle
                e = new Ellipse { 
                    Margin = new Thickness(approachLeft, approachTop, 0, 0), 
                    Width = approachDiameter, 
                    Height = approachDiameter, 
                    StrokeThickness = 2, 
                    Stroke = new SolidColorBrush(Colors.Black), 
                    HorizontalAlignment = HorizontalAlignment.Left, 
                    VerticalAlignment = VerticalAlignment.Top };
                view.Children.Add(e);
            } else
            {
                // Make it smaller.
                e.Margin = new Thickness(approachLeft, approachTop, 0, 0);
                e.Width = approachDiameter;
                e.Height = approachDiameter;
                b.Content = $"{(order%9) + 1}";
            }
            return true;
        }

        public override void DisposeElements(Grid g)
        {
            // Get rid of hitobject.
            base.DisposeElements(g);
            g.Children.Remove(e);
            g.Children.Remove(b);
            if (!interacted) game.AddHit(HitResult.Miss, order);
        }

        private void Clicked(object sender, RoutedEventArgs e)
        {
            var hit = EvaluateHit(game.GetTime());
            if (game.AddHit(hit, order)) interacted = true;
        }
    }

    public interface HitObjectProvider
    {

    }

    public static class ext {
        public static T QuickSelect<T>(this IList<T> arr, int k, Func<T, T, int> comparrison, int l = 0, int r = -1)
        {
            while (true) {
                int p = l;
                if (r == -1) r = arr.Count - 1;
                T itm = arr[p];
                for (int x = l + 1; x <= r; x++)
                {
                    if (comparrison(arr[x], itm) < 0)
                    {
                        arr[p] = arr[x];
                        T temp = arr[++p];
                        arr[x] = temp;
                        arr[p] = itm;
                    }
                }
                if (p > k) r = p - 1;
                else if (p == k) return arr[p];
                else l = p+1;
            }
        }

        public static T GetMedian<T>(this IList<T> els) where T : IComparable<T>
        {
            return els.QuickSelect(els.Count / 2, (x, y) => x.CompareTo(y));
        }

        public static double TotalSeconds(this MetricTimeSpan ms) => ms.TotalMicroseconds / 1E6;
    }

    public class Scoring
    {
        public int combo;
        public long score;
    }

    public interface IAcceptsScoreUpdates
    {
        public bool AddHit(HitResult hr, int order);
        public TimeSpan GetTime();
    }

    class Note
    {
        public int num;
        public TimeSpan startTime;
        public TimeSpan duration;
        public byte velocity;
        public byte noteNumber;
    }

    class Track
    {
        public List<Note> notes;
        public string name;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IAcceptsScoreUpdates
    {
        TimeSpan currentTime = TimeSpan.Zero;
        TimeSpan gameplayTime = TimeSpan.Zero;
        DateTime lastUpdate = DateTime.Now;

        public MainWindow()
        {
            InitializeComponent();
            Circle.buttonStyle = this.FindResource("GlassButton") as Style;

        }
        string Filepath;
        private void OpenFile(object sender, RoutedEventArgs e)
        {
            VistaOpenFileDialog dialog = new VistaOpenFileDialog();
            dialog.DefaultExt = ".mid";
            dialog.AddExtension = true;
            dialog.Filter = "MIDI File|*.mid";
            dialog.ShowDialog();
            Filepath = dialog.FileName;
        }
        


        private double medianNoteDuration(List<Note> track) => track.Select(x => x.duration.TotalMilliseconds).ToList().GetMedian();
        private double medianNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.startTime - x.First.startTime).Select(x => x.TotalMilliseconds).ToList().GetMedian();
        private double meanNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.startTime - x.First.startTime).Select(x => x.TotalMilliseconds).Average();
        private double minNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.startTime - x.First.startTime).Select(x => x.TotalMilliseconds).Where(x => x != 0).Min();
        private double maxNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.startTime - x.First.startTime).Select(x => x.TotalMilliseconds).Max();


        IEnumerable<HitObject> hitObjects; // All elements in chronologial order. Temporary

        public class Generator
        {
            double playfieldLength;
            double playfieldHeight;
            double aspectRatio;
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
            
            double overallDifficulty; // Helps determine how far each note should be placed. When below 1, the circles should not overlap. Max is 5
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

            public double GetNewLength()
            {
                double proposed = overallDifficulty / (5.0 * 2.0 * DifficultyRadius);
                if (proposed > 1.5) return 1.5;
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
                    // Get new length;
                    double newLength = GetNewLength();
                    // get previous radius
                    HitObject previous = previousHitObjects.Last.Value;
                    double previousRadius = previous.PolarPosition.magnitude;
                    double previousAngle = previous.PolarPosition.Angle; // radians. Guaranteed to be positive.

                    double angleDifference = Math.Acos((Math.Pow(newLength, 2.0) - Math.Pow(previousRadius, 2.0) - Math.Pow(DifficultyRadius, 2.0)) / (-2.0 * previousRadius * DifficultyRadius));
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

            Track RemoveDuplicateNotes(Track track)
            {
                Track newTrack = new Track();
                newTrack.notes = new List<Note>();
                newTrack.name = track.name;
                TimeSpan previousTimeSpan = new TimeSpan(-1);
                foreach(Note note in track.notes)
                {
                    if (note.startTime == previousTimeSpan) continue;
                    newTrack.notes.Add(note);
                    previousTimeSpan = note.startTime;
                }
                return newTrack;
            }
            public IEnumerable<HitObject> GetHitObjects()
            {
                Track t = RemoveDuplicateNotes(game.landmarks[0]);
                List<Note> n = t.notes;
                double time = 0;
                foreach (Note note in n) {
                    Circle c = new Circle(game) { start = note.startTime };
                    c.SetPolarPosition(getNextPosition(), aspectRatio);
                    yield return c; 
                }
            }

        }
        public Generator generatorInstance;
        private List<HitObject> displaying = new List<HitObject>();
        IEnumerator<HitObject> HitObjectEnumerator;
        private void PerformGameUpdate(TimeSpan time)
        {
            lastUpdate = DateTime.Now;
            while (HitObjectEnumerator.Current.Render(Playfield,time))
            {
                displaying.Add(HitObjectEnumerator.Current);
                HitObjectEnumerator.Current.order = currentObjIdx++;
                HitObjectEnumerator.MoveNext();
            }
            foreach (var obj in displaying) obj.Render(Playfield, time);
            var toRemove = displaying.Where(x => x.CanDispose(time));
            foreach (var o in toRemove) o.DisposeElements(Playfield);
            displaying.RemoveAll(x => toRemove.Contains(x));
        }

        private double lengthSeconds;
        private List<Track> FindLandmarks(MidiFile file)
        {
            var chunks = from TrackChunk midichunk in file.Chunks
                         from midiitem in midichunk.Events
                         where midiitem.EventType == MidiEventType.SequenceTrackName
                         select (midichunk, ((SequenceTrackNameEvent)midiitem).Text);
            List<Track> tracks = new List<Track>();
            TempoMap tempoMap = file.GetTempoMap();
            lengthSeconds = (file
                                .GetTimedEvents()
                                .LastOrDefault(e => e.Event is NoteOffEvent)
                                ?.TimeAs<MetricTimeSpan>(tempoMap) ?? new MetricTimeSpan()).TotalSeconds();
            foreach  (var chunk in chunks)
            {
                List<Note> notes = new List<Note>();
                long currentTime = 0;
                long first = -1;
                foreach (MidiEvent a in chunk.midichunk.Events)
                {
                    currentTime += a.DeltaTime;
                    var currTimeTS = TimeConverter.ConvertTo<MetricTimeSpan>(currentTime, tempoMap);
                     switch (a)
                     {
                        case NoteOnEvent ev:
                            notes.Add(new Note { num = ev.NoteNumber, startTime = currTimeTS, velocity = ev.Velocity, noteNumber = ev.NoteNumber });
                            if (first == -1)
                                first = currentTime;
                            break;
                        case NoteOffEvent ev:
                            for (int i = notes.Count - 1; i > 0; i--)
                                if (notes[i].num == ev.NoteNumber)
                                {
                                    notes[i].duration = (TimeSpan)currTimeTS - notes[i].startTime;
                                    break;
                                }
                            break;
                     }
                }
                tracks.Add(new Track { notes = notes, name = chunk.Text });
            }
            var preorder = tracks.Where(x => x.notes.Select(x => x.startTime).Distinct().Count() > lengthSeconds / 2 && x.notes.Select(x => x.startTime).Distinct().Count() < lengthSeconds * 8).Select(x => (x, scoreTrack(x))).OrderByDescending(x => x.Item2).ToList();
            var ordered = preorder.Select(x => x.x).ToList();
            return ordered;
        }

        private double scoreTrack(Track t)
        {
            double medianSpacing = medianNoteSpacing(t.notes);
            double meanSpacing = meanNoteSpacing(t.notes);
            double maxSpacing = maxNoteSpacing(t.notes);
            double minSpacing = minNoteSpacing(t.notes);
            double spacingRange = maxSpacing - minSpacing;
            double firstNoteStart = t.notes.First().startTime.Ticks;
            double extremespeedModifier = 0;
            if (minSpacing < 50) extremespeedModifier = 1;
            return (medianSpacing + meanSpacing) * (medianSpacing + meanSpacing) * t.notes.Count - (maxSpacing * maxSpacing) - spacingRange * 0.2 - firstNoteStart - (maxSpacing - minSpacing) * 100 * extremespeedModifier;
        }

        private Playback _playback;
        private OutputDevice _outputDevice;
        Timer gameplayTimer;
        System.Diagnostics.Stopwatch gameTimer = new System.Diagnostics.Stopwatch();
        const int timerTick = 5;
        List<Track> landmarks;
        int currentObjIdx;
        private void Start(object sender, RoutedEventArgs e)
        {
            currentTime = TimeSpan.Zero;
            gameplayTime = TimeSpan.Zero;
            MidiFile midiFile;
            try
            {
                midiFile = MidiFile.Read(Filepath);
            } catch (Exception)
            {
                ScoreTextBlock.Text = "Please open a midi file before starting.";
                return;
            }
            this.ScoreTextBlock.Text = "";
            this.landmarks = FindLandmarks(midiFile);
            currentObjIdx = 0;
            currentScoreIdx = -1;
            landmarks = FindLandmarks(midiFile);
            scoring = new Scoring();
            Random r = new Random();
            generatorInstance = new Generator(Playfield, this);
            hitObjects = generatorInstance.GetHitObjects();
            HitObjectEnumerator = hitObjects.GetEnumerator();
            HitObjectEnumerator.MoveNext();
            //hitObjects = landmarks.First().notes.Select(x => getSecondsForEvent(x.start)).Select(x => TimeSpan.FromSeconds(x)).Select(x => new Circle(this) { position = new Vector2((float) r.NextDouble(), (float) r.NextDouble()), start = x }).Select(x => x as HitObject).ToList();
            _outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");

            _playback = midiFile.GetPlayback(_outputDevice);
            PlaybackCurrentTimeWatcher.Instance.AddPlayback(_playback,TimeSpanType.Metric);
            PlaybackCurrentTimeWatcher.Instance.CurrentTimeChanged += OnCurrentTimeChanged;
            
            _playback.Speed = 1;
            gameplayTimer = new Timer((t) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PerformGameUpdate(GetTime());
                });
            }, null, timerTick, timerTick);
            gameTimer.Restart();
            _playback.Start();
            PlaybackCurrentTimeWatcher.Instance.Start();

            _playback.Finished += Finished;
            
        }

        private void Finished(object sender, EventArgs e)
        {
            _outputDevice.Dispose();
            _playback.Dispose();
        }
        
        private void OnCurrentTimeChanged(object sender, PlaybackCurrentTimeChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                currentTime = e.Times.First().Time as MetricTimeSpan;
                var diff = (currentTime - GetTime());
                if (diff.TotalSeconds > 0.1)
                {
                    MessageBox.Show("System clock and midi clock are out of sync.", "Error!!!");
                }
            });
        }
        Scoring scoring;
        int currentScoreIdx;
        public bool AddHit(HitResult hr, int order)
        {
            if (order == currentScoreIdx + 1)
            {
                if (hr <= HitResult.Meh)
                    scoring.combo = 0;
                else
                    scoring.combo++;
                scoring.score += scoring.combo * (int)hr;
                generatorInstance.ProcessHitResult(hr);
                ScoreTextBlock.Text = $"Score: {scoring.score}, Combo: {scoring.combo}";
                currentScoreIdx = order;
                return true;
            }
            //MessageBox.Show("XD");
            return false;
        }

        public TimeSpan GetTime()
        {
            return gameTimer.Elapsed;
        }


    }
}
