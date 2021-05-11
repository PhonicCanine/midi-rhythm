using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace MidiVersion
{
    public class HitObject
    {
        public IAcceptsScoreUpdates game;
        public TimeSpan start;
        public TimeSpan resultStarted;
        public HitResult result;
        protected bool interacted = false;
        public int order;
        public Note associatedNote;
        public HitObject(IAcceptsScoreUpdates attachedTo, Note associated)
        {
            game = attachedTo;
            this.associatedNote = associated;
        }
        public virtual HitResult EvaluateHit(TimeSpan hitTime) => Math.Abs(hitTime.TotalSeconds - start.TotalSeconds) switch
        {
            0 => HitResult.Perfect,
            <= 0.1 => HitResult.Great,
            <= 0.15 => HitResult.OK,
            <= 0.4 => HitResult.Meh,
            _ => HitResult.Miss
        };

        public virtual Color HitResultColors(HitResult hr) => hr switch
        {
            HitResult.Perfect => Colors.SkyBlue,
            HitResult.Great => Colors.Green,
            HitResult.OK => Colors.Yellow,
            HitResult.Meh => Colors.Orange,
            HitResult.Miss => Colors.Red,
            _ => Colors.Gray
        };
        /// <summary>
        /// x = 1 -> rightmost, x = -1 -> leftmost
        /// y = 1 -> topmost, y = -1 -> bottommost
        /// Note that polarPosition set ==> position set but not the converse.
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
        public virtual bool CanDispose(TimeSpan atTime) => atTime > start + TimeSpan.FromSeconds(0.6) || interacted && atTime > resultStarted + TimeSpan.FromSeconds(0.2);
        protected static Vector2 GetLocationRelative(Grid view, Vector2 rel) => new Vector2((float)(view.ActualWidth * ((rel.X / 2) + 0.5)), (float)(view.ActualHeight * ((rel.Y / 2) + 0.5)));
        public virtual void DisposeElements(Grid g)
        {

        }
    }
}
