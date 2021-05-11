using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MidiVersion
{
    public class Circle : HitObject
    {
        const float diameter = 75; // Diameter of hitcircle (px).
        public static Style buttonStyle;
        Button b;
        Ellipse e;
        Label textBlock;
        TimeSpan mostRecent = TimeSpan.Zero;

        public Circle(IAcceptsScoreUpdates game, Note associated) : base(game, associated)
        {

        }
        public override bool Render(Grid view, TimeSpan forTime)
        {
            base.Render(view, forTime);
            mostRecent = forTime;
            float bLeft = GetLocationRelative(view, position).X;
            float left = bLeft - diameter / 2;
            float bTop = (float)view.ActualHeight - (GetLocationRelative(view, position).Y);
            float top = (float)view.ActualHeight - (GetLocationRelative(view, position).Y + diameter / 2);

            if (forTime > resultStarted && resultStarted != TimeSpan.Zero)
            {
                if (textBlock is null)
                {
                    textBlock = new Label
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        FontSize = 30
                    };
                    textBlock.Margin = new Thickness(e.Margin.Left, e.Margin.Top, view.ActualWidth - e.Margin.Left - e.ActualWidth, view.ActualHeight - e.Margin.Top - e.ActualHeight);
                    view.Children.Insert(view.Children.IndexOf(e) + 1, textBlock);
                }
                textBlock.Content = $"{(int)result}";
                textBlock.Foreground = new SolidColorBrush(this.HitResultColors(result));
                textBlock.Visibility = Visibility.Visible;
                textBlock.Opacity = Math.Min((forTime - resultStarted).TotalSeconds * 10, 1);
                return true;
            }

            if (!interacted && forTime >= start + TimeSpan.FromSeconds(0.4)) Hit(HitResult.Miss, forTime);

            double t = Math.Max(0, (start - forTime).TotalSeconds);
            if (t > 0.5)
            {
                return false;
            }


            float approachDiameter = (float)(diameter * (t * 4 + 1));
            float approachLeft = GetLocationRelative(view, position).X - approachDiameter / 2;
            float approachTop = (float)view.ActualHeight - (GetLocationRelative(view, position).Y + approachDiameter / 2);


            if (b is null)
            {
                b = new Button
                {
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
                e = new Ellipse
                {
                    Margin = new Thickness(approachLeft, approachTop, 0, 0),
                    Width = approachDiameter,
                    Height = approachDiameter,
                    StrokeThickness = 2,
                    Stroke = new SolidColorBrush(Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
                view.Children.Add(e);
            }
            else
            {
                // Make it smaller.
                e.Margin = new Thickness(approachLeft, approachTop, 0, 0);
                e.Width = approachDiameter;
                e.Height = approachDiameter;
                b.Content = $"{(order % 9) + 1}";
            }
            return true;
        }

        public override void DisposeElements(Grid g)
        {
            // Get rid of hitobject.
            base.DisposeElements(g);
            g.Children.Remove(e);
            g.Children.Remove(b);
            g.Children.Remove(textBlock);
        }

        private void Hit(HitResult hit, TimeSpan time)
        {
            if (game.AddHit(hit, order))
            {
                interacted = true;
                result = hit;
                resultStarted = mostRecent;
            }
        }

        private void Clicked(object sender, RoutedEventArgs e)
        {
            var hit = EvaluateHit(game.GetTime());
            Hit(hit, game.GetTime());
        }
    }

}
