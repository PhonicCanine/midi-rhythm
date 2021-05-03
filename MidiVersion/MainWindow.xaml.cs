using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using Melanchall.DryWetMidi.Interaction;
using Ookii.Dialogs.Wpf;

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
        public TimeSpan start;
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
        public virtual void Render(Grid view, TimeSpan forTime)
        {

        }
        public virtual bool CanDispose(TimeSpan atTime) => atTime > start + TimeSpan.FromSeconds(0.5);
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
        public override void Render(Grid view, TimeSpan forTime)
        {
            base.Render(view, forTime);
            double t = Math.Max(0,(start - forTime).TotalSeconds);
            if (t > 0.5)
            {
                return;
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
                e = new Ellipse { Margin = new Thickness(approachLeft, approachTop, 0, 0), Width = approachDiameter, Height = approachDiameter, StrokeThickness = 2, Stroke = new SolidColorBrush(Colors.Black), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                view.Children.Add(e);
            } else
            {
                e.Margin = new Thickness(approachLeft, approachTop, 0, 0);
                e.Width = approachDiameter;
                e.Height = approachDiameter;
            }
            
        }

        public override void DisposeElements(Grid g)
        {
            base.DisposeElements(g);
            g.Children.Remove(e);
            g.Children.Remove(b);
        }

        private void Clicked(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
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

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TimeSpan currentTime = TimeSpan.Zero;

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
            public byte velocity;
        }

        class Track
        {
            public List<Note> notes;
            public string name;
        }


        private long medianNoteDuration(List<Note> track) => track.Select(x => x.duration).ToList().GetMedian();
        private long medianNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.start - x.First.start).ToList().GetMedian();
        private double meanNoteSpacing(List<Note> track) => track.Zip(track.Skip(1)).Select(x => x.Second.start - x.First.start).ToList().Average();


        List<HitObject> objects = new List<HitObject>();

        private void PerformGameUpdate(TimeSpan time)
        {
            var toDelete = objects.Where(x => x.CanDispose(time));
            foreach (var r in toDelete) { r.DisposeElements(GameGrid); }
            objects.RemoveAll(x => x.CanDispose(time));
            foreach (var o in objects) o.Render(GameGrid, time);
        }

        private List<Track> FindLandmarks(MidiFile file)
        {
            var chunks = from TrackChunk midichunk in file.Chunks
                         from midiitem in midichunk.Events
                         where midiitem.EventType == MidiEventType.SequenceTrackName
                         select (midichunk, ((SequenceTrackNameEvent)midiitem).Text);
            List<Track> tracks = new List<Track>();
            TempoMap tempoMap = file.GetTempoMap();
            double seconds = (file
                                .GetTimedEvents()
                                .LastOrDefault(e => e.Event is NoteOffEvent)
                                ?.TimeAs<MetricTimeSpan>(tempoMap) ?? new MetricTimeSpan()).TotalSeconds();
            long maxTime = 0;
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
                            maxTime = Math.Max(maxTime, currentTime);
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
            var ordered = tracks.Where(x => x.notes.Select(x => x.start).Distinct().Count() > seconds / 2 && x.notes.Select(x => x.start).Distinct().Count() < seconds * 8).OrderByDescending(x => medianNoteSpacing(x.notes) + meanNoteSpacing(x.notes)).ToList();
            return ordered;
        }

        private Playback _playback;
        private OutputDevice _outputDevice;
        Timer t;
        private void Start(object sender, RoutedEventArgs e)
        {
            currentTime = TimeSpan.Zero;
            t = new Timer((t) =>
            {
                currentTime += TimeSpan.FromMilliseconds(10);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PerformGameUpdate(currentTime);
                });
            }, null, 10, 10);
            var midiFile = MidiFile.Read(Filepath);
            var landmarks = FindLandmarks(midiFile);
            objects = Enumerable.Range(0,100).Select(x => new Circle { position = Vector2.Zero, start = TimeSpan.FromSeconds(x * 4) } as HitObject).ToList();//landmarks.First().notes.Select(x => new Circle { position = Vector2.Zero, start = x. });
            _outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");

            _playback = midiFile.GetPlayback(_outputDevice);
            _playback.EventPlayed += OnEventPlayed;
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                Title = $"{_playback.GetCurrentTime(TimeSpanType.Metric)}";
                currentTime = _playback.GetCurrentTime(TimeSpanType.Metric) as MetricTimeSpan;
            });
            
        }
    }
}
