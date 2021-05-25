using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using Melanchall.DryWetMidi.Interaction;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LengthConverter = Melanchall.DryWetMidi.Interaction.LengthConverter;

namespace MidiVersion
{

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

    public class Track
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
        public IGenerator generatorInstance;
        public IDifficultyManager difficultyManager;
        public MainWindow()
        {
            InitializeComponent();
            Circle.buttonStyle = this.FindResource("GlassButton") as Style;
            generatorInstance = new Generator();
            difficultyManager = new SecondAlternateDifficultyManager();//DefaultDifficultyManager();
        }
        string Filepath;
        private void OpenFile(object sender, RoutedEventArgs e)
        {
            OpenMidiFile();
        }

        void OpenMidiFile()
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

        
        
        private List<HitObject> displaying = new List<HitObject>();
        IEnumerator<HitObject> HitObjectEnumerator;
        private void PerformGameUpdate(TimeSpan time)
        {
            int concurrent = 0;
            while (HitObjectEnumerator.Current.Render(Playfield,time) && _playback != null && ++concurrent < 5)
            {
                if (HitObjectEnumerator.Current.position != Generator.NULL_VECTOR)
                {
                    displaying.Add(HitObjectEnumerator.Current);
                    HitObjectEnumerator.Current.order = currentObjIdx++;
                }
                    
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
                         select (midichunk, "Track");
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
                Track track = new Track { name = chunk.Item2 };
                if (chunk.midichunk.Events.Where(x => x.EventType == MidiEventType.SequenceTrackName).Any())
                    track.name = (chunk.midichunk.Events.Where(x => x.EventType == MidiEventType.SequenceTrackName).First() as SequenceTrackNameEvent).Text;
                foreach (MidiEvent a in chunk.midichunk.Events)
                {
                    currentTime += a.DeltaTime;
                    var currTimeTS = TimeConverter.ConvertTo<MetricTimeSpan>(currentTime, tempoMap);
                     switch (a)
                     {
                        case NoteOnEvent ev:
                            notes.Add(new Note { num = ev.NoteNumber, startTime = currTimeTS, velocity = ev.Velocity, noteNumber = ev.NoteNumber, tempo = tempoMap.GetTempoAtTime(currTimeTS).BeatsPerMinute, fromTrack = track });
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
                track.notes = notes;
                tracks.Add(track);
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
        public List<Track> landmarks;
        int currentObjIdx;
        private void Start(object sender, RoutedEventArgs e)
        {
            StartGame();
        }

        private void Finished(object sender, EventArgs e)
        {
            _outputDevice.Dispose();
            _playback.Dispose();
            gameTimer.Stop();
            gameplayTimer.Dispose();
            _outputDevice = null;
            _playback = null;
            PlaybackCurrentTimeWatcher.Instance.RemoveAllPlaybacks();
            Application.Current.Dispatcher.Invoke(() =>
            {
                SurveyWindow.Visibility = Visibility.Visible;
                string text = $"data-v2:manager:{difficultyManager.GetName()},difficulty:{difficultyManager.GetDifficulty()},score:{scoring.score},originallyselecteddifficulty:{selectedDifficulty}";
                SurveyText.Text = BitConverter.ToString(Encoding.Default.GetBytes(text)).Replace("-", "");
                Playfield.Children.Clear();
            });
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
                    scoring.combo = 1;
                else
                    scoring.combo++;
                scoring.score += scoring.combo * (int)hr;
                ScoreTextBlock.Text = $"Score: {scoring.score}, Combo: {scoring.combo - 1}";
                currentScoreIdx = order;
                difficultyManager.AddHit(hr);
                return true;
            }
            
            return false;
        }

        public TimeSpan GetTime()
        {
            return gameTimer.Elapsed;
        }

        private void BeginSurvey(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://forms.office.com/r/vYzzKVVSgL");
        }

        private void CloseSurveyWindow(object sender, RoutedEventArgs e)
        {
            SurveyWindow.Visibility = Visibility.Collapsed;
            DifficultySelectWindow.Visibility = Visibility.Visible;
        }

        void StartGame()
        {
            currentTime = TimeSpan.Zero;
            MidiFile midiFile;
            try
            {
                midiFile = MidiFile.Read(Filepath);
            }
            catch (Exception)
            {
                ScoreTextBlock.Text = "Please open a midi file before starting.";
                DifficultySelectWindow.Visibility = Visibility.Visible;
                return;
            }
            this.ScoreTextBlock.Text = "";
            this.landmarks = FindLandmarks(midiFile);
            currentObjIdx = 0;
            currentScoreIdx = -1;
            landmarks = FindLandmarks(midiFile);
            scoring = new Scoring();
            Random r = new Random();

            generatorInstance.Initialize(Playfield, this);
            difficultyManager.generator = generatorInstance;

            hitObjects = generatorInstance.GetHitObjects();
            HitObjectEnumerator = hitObjects.GetEnumerator();
            HitObjectEnumerator.MoveNext();
            _outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");

            _playback = midiFile.GetPlayback(_outputDevice);
            
            PlaybackCurrentTimeWatcher.Instance.AddPlayback(_playback, TimeSpanType.Metric);
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

        List<IDifficultyManager> DifficultyManagers;

        void BeginWithDifficulty(double difficulty)
        {
            if (Filepath is null or "") return;
            DifficultyManagers = new List<IDifficultyManager>
            {
                new DefaultDifficultyManager(),
                new SecondAlternateDifficultyManager(),
                new PIDDifficultyManager(),
                new ConstantDifficultyManager(),
                new ConstantDifficultyManager(),
                new ConstantDifficultyManager()
            };

            Random r = new Random();
            int idx = r.Next(0, DifficultyManagers.Count);
            difficultyManager = DifficultyManagers[idx];
            difficultyManager.SetInitialDifficulty(difficulty);
            DifficultySelectWindow.Visibility = Visibility.Collapsed;
            StartGame();
        }

        int selectedDifficulty = 0;

        private void StartEasy(object sender, RoutedEventArgs e)
        {
            OpenMidiFile();
            selectedDifficulty = 0;
            BeginWithDifficulty(0);
        }

        private void StartNormal(object sender, RoutedEventArgs e)
        {
            OpenMidiFile();
            selectedDifficulty = 1;
            BeginWithDifficulty(0.3);
        }

        private void StartHard(object sender, RoutedEventArgs e)
        {
            OpenMidiFile();
            selectedDifficulty = 2;
            BeginWithDifficulty(0.7);
        }
    }
}
