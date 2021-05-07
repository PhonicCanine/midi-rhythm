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

    public class HitObject
    {
        public IAcceptsScoreUpdates game;
        public TimeSpan start;
        protected bool interacted = false;
        public HitObject(IAcceptsScoreUpdates attachedTo)
        {
            game = attachedTo;
        }
        public virtual HitResult EvaluateHit(TimeSpan hitTime) => Math.Abs(hitTime.TotalSeconds - start.TotalSeconds) switch
        {
            0 => HitResult.Perfect,
            <= 0.1 => HitResult.Great,
            <= 0.15 => HitResult.OK,
            <= 0.2 => HitResult.Meh,
            _ => HitResult.Miss
        };
        /// <summary>
        /// x = 1 -> rightmost, x = -1 -> leftmost
        /// y = 1 -> topmost, y = -1 -> bottommost
        /// </summary>
        public Vector2 position;
        public virtual bool Render(Grid view, TimeSpan forTime)
        {
            return false;
        }
        
        public virtual bool CanDispose(TimeSpan atTime) => atTime > start + TimeSpan.FromSeconds(0.5) || interacted; // If the time is late enough, we can delete the hitObject.
        protected static Vector2 GetLocationRelative(Grid view, Vector2 rel) => new Vector2((float)(view.ActualWidth * ((rel.X / 2) + 0.5)), (float)(view.ActualHeight * ((rel.Y / 2) + 0.5)));
        public virtual void DisposeElements(Grid g)
        {

        }
    }

    public class Circle: HitObject
    {
        const float diameter = 75;
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
                b = new Button { Style = buttonStyle, Background = new SolidColorBrush(Colors.Turquoise), Height = diameter, Width = diameter, Margin = new Thickness(left, top, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                b.Click += Clicked;
                view.Children.Add(b);
            }
            if (e is null)
            {
                // Add approach circle
                e = new Ellipse { Margin = new Thickness(approachLeft, approachTop, 0, 0), Width = approachDiameter, Height = approachDiameter, StrokeThickness = 2, Stroke = new SolidColorBrush(Colors.Black), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                view.Children.Add(e);
            } else
            {
                // Make it smaller.
                e.Margin = new Thickness(approachLeft, approachTop, 0, 0);
                e.Width = approachDiameter;
                e.Height = approachDiameter;
            }
            return true;
        }

        public override void DisposeElements(Grid g)
        {
            // Get rid of hitobject.
            base.DisposeElements(g);
            g.Children.Remove(e); // Remove approach circle
            g.Children.Remove(b); // Remove actual hitobject
        }

        private void Clicked(object sender, RoutedEventArgs e)
        {
            var hit = EvaluateHit(game.GetTime());
            game.AddHit(hit);
            interacted = true;
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
        public void AddHit(HitResult hr);
        public TimeSpan GetTime();
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IAcceptsScoreUpdates
    {
        public double diffCircleRadius = 300;
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

        class Note
        {
            public int num;
            public long start;
            public TimeSpan startTime;
            public long duration;
            public byte velocity; // volume/how strongly the note was hit in the midi file.
        }

        class Track
        {
            public List<Note> notes;
            public string name;
        }


        private long medianNoteDuration(List<Note> track) => track.Select(x => x.duration).ToList().GetMedian();
        private long medianNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.start - x.First.start).ToList().GetMedian();
        private double meanNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.start - x.First.start).Average();
        private double minNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.start - x.First.start).Where(x => x != 0).Min();
        private double maxNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.start - x.First.start).Max();


        IEnumerable<HitObject> hitObjects; // All elements in chronologial order. Temporary

        private class Generator
        {
            public bool hasNext()
            {
                return true;
            }
            public IEnumerable<HitObject> GetHitObjects(MainWindow game, List<Track> landmarks)
            {
                Track t = landmarks[0];
                List<Note> n = t.notes;
                double time = 0;
                foreach (Note note in n) {
                    yield return new Circle(game) { position = Vector2.Zero, start = TimeSpan.FromMilliseconds(note.start) }; 
                }
            }
        }
        private Generator generatorInstance;
        private List<HitObject> displaying = new List<HitObject>();
        IEnumerator<HitObject> HitObjectEnumerator;
        private void PerformGameUpdate(TimeSpan time)
        {
            lastUpdate = DateTime.Now;
            while (HitObjectEnumerator.Current.Render(GameGrid,time))
            {
                displaying.Add(HitObjectEnumerator.Current);
                HitObjectEnumerator.MoveNext();
            }
            foreach (var obj in displaying) obj.Render(GameGrid, time);
            var toRemove = displaying.Where(x => x.CanDispose(time));
            foreach (var o in toRemove) o.DisposeElements(GameGrid);
            displaying.RemoveAll(x => toRemove.Contains(x));
        }

        private long lengthEvents;
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
            lengthEvents = 0;
            foreach  (var chunk in chunks)
            {
                List<Note> notes = new List<Note>();
                long currentTime = 0;
                long first = -1;
                foreach (MidiEvent a in chunk.midichunk.Events)
                {
                    currentTime += a.DeltaTime;
                     switch (a)
                     {
                        case NoteOnEvent ev:
                            notes.Add(new Note { num = ev.NoteNumber, start = currentTime, velocity = ev.Velocity });
                            if (first == -1)
                                first = currentTime;
                            break;
                        case NoteOffEvent ev:
                            lengthEvents = Math.Max(lengthEvents, currentTime);
                            for (int i = notes.Count - 1; i > 0; i--)
                                if (notes[i].num == ev.NoteNumber)
                                {
                                    notes[i].duration = currentTime - notes[i].start;
                                    break;
                                }
                            break;
                     }
                }
                tracks.Add(new Track { notes = notes, name = chunk.Text });
            }
            var preorder = tracks.Where(x => x.notes.Select(x => x.start).Distinct().Count() > lengthSeconds / 2 && x.notes.Select(x => x.start).Distinct().Count() < lengthSeconds * 8).Select(x => (x, scoreTrack(x))).OrderByDescending(x => x.Item2).ToList();
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
            double firstNoteStart = t.notes.First().start;
            double extremespeedModifier = 0;
            if (getSecondsForEvent((int)minSpacing) < 0.05) extremespeedModifier = 1;
            return (medianSpacing + meanSpacing) * (medianSpacing + meanSpacing) * t.notes.Count - (maxSpacing * maxSpacing) - spacingRange * 0.2 - firstNoteStart - (maxSpacing - minSpacing) * 100 * extremespeedModifier;
        }

        private double getSecondsForEvent(long eventTime) => eventTime / (double)lengthEvents * lengthSeconds;

        private Playback _playback;
        private OutputDevice _outputDevice;
        Timer t;

        /// <summary>
        /// Starts the game
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        const int timerTick = 5;
        private void Start(object sender, RoutedEventArgs e)
        {
            currentTime = TimeSpan.Zero;
            gameplayTime = TimeSpan.Zero;
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            t = new Timer((t) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PerformGameUpdate(gameplayTime);
                    sw.Stop();
                    gameplayTime += TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
                    sw.Restart();
                });
            }, null, timerTick, timerTick);

            var midiFile = MidiFile.Read(Filepath);
            List<Track> landmarks = FindLandmarks(midiFile);
            scoring = new Scoring();
            Random r = new Random();
            generatorInstance = new Generator();
            hitObjects = generatorInstance.GetHitObjects(this, landmarks);
            HitObjectEnumerator = hitObjects.GetEnumerator();
            HitObjectEnumerator.MoveNext();
            //hitObjects = landmarks.First().notes.Select(x => getSecondsForEvent(x.start)).Select(x => TimeSpan.FromSeconds(x)).Select(x => new Circle(this) { position = new Vector2((float) r.NextDouble(), (float) r.NextDouble()), start = x }).Select(x => x as HitObject).ToList();
            _outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");

            _playback = midiFile.GetPlayback(_outputDevice);
            _playback.EventPlayed += OnEventPlayed;
            _playback.Speed = 1;
            _playback.Start();

            _playback.Finished += Finished;
            
        }

        private void Finished(object sender, EventArgs e)
        {
            _outputDevice.Dispose();
            _playback.Dispose();
        }

        private void OnEventPlayed(object sender, MidiEventPlayedEventArgs e)
        {
            if ((DateTime.Now - lastUpdate).TotalSeconds > 0.1)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    currentTime = _playback.GetCurrentTime(TimeSpanType.Metric) as MetricTimeSpan;
                    lastUpdate = DateTime.Now;
                    gameplayTime = currentTime;
                    var diff = (currentTime - gameplayTime);
                    if (diff.TotalSeconds > 1)
                    {
                        MessageBox.Show("System clock and midi clock are out of sync.", "Error!!!");
                    }
                });
            
        }
        Scoring scoring;
        public void AddHit(HitResult hr)
        {
            if (hr <= HitResult.Miss)
                scoring.combo = 0;
            else
                scoring.combo++;
            scoring.score += scoring.combo * (int)hr;
            ScoreTextBlock.Text = $"Score: {scoring.score}, Combo: {scoring.combo}";
        }

        public TimeSpan GetTime()
        {
            return gameplayTime;
        }


    }
}
