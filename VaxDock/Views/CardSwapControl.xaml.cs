using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VaxDrive.VaxDock.Views;

public partial class CardSwapControl : UserControl
{
    public event EventHandler? SyncClicked;
    public event EventHandler? ViewClicked;
    public event EventHandler? PrepareClicked;

    private List<Border> _cards = new();
    private bool _isAnimating = false;

    public CardSwapControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cards = new List<Border> { CardSync, CardView, CardPrepare };
        UpdateCardPositions(animate: false);
    }

    private void UpdateCardPositions(bool animate)
    {
        int total = _cards.Count;
        for (int i = 0; i < total; i++)
        {
            var card = _cards[i];
            Panel.SetZIndex(card, total - i);

            double targetScale = 1.0 - (i * 0.08);
            double targetY = i * -40;

            if (card.RenderTransform is not TransformGroup tg)
            {
                tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(1, 1));
                tg.Children.Add(new TranslateTransform(0, 0));
                card.RenderTransform = tg;
            }

            var scale = (ScaleTransform)tg.Children[0];
            var trans = (TranslateTransform)tg.Children[1];

            Canvas.SetLeft(card, 0);
            Canvas.SetTop(card, 0);

            if (animate)
            {
                var duration = TimeSpan.FromSeconds(0.4);
                var ease = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut };

                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScale, duration) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScale, duration) { EasingFunction = ease });
                trans.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(targetY, duration) { EasingFunction = ease });
            }
            else
            {
                scale.ScaleX = targetScale;
                scale.ScaleY = targetScale;
                trans.Y = targetY;
            }
        }
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isAnimating || sender is not Border clickedCard) return;

        int index = _cards.IndexOf(clickedCard);
        if (index == 0)
        {
            // Front card clicked -> Action with visual reaction
            string tag = clickedCard.Tag as string ?? "";
            
            var tg = (TransformGroup)clickedCard.RenderTransform;
            var scale = (ScaleTransform)tg.Children[0];
            
            // Quick physical "press" animation
            var pressAnimX = new DoubleAnimation(0.95, TimeSpan.FromMilliseconds(100)) 
            { 
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var pressAnimY = new DoubleAnimation(0.95, TimeSpan.FromMilliseconds(100)) 
            { 
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            
            // Only attach the completion event to ONE of the animations to prevent double-firing
            pressAnimX.Completed += (s, ev) =>
            {
                if (tag == "Sync") SyncClicked?.Invoke(this, EventArgs.Empty);
                else if (tag == "View") ViewClicked?.Invoke(this, EventArgs.Empty);
                else if (tag == "Prepare") PrepareClicked?.Invoke(this, EventArgs.Empty);
            };
            
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pressAnimX);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pressAnimY);
        }
        else
        {
            // Background card clicked -> Swap cycle
            _isAnimating = true;

            var frontCard = _cards[0];
            var tg = (TransformGroup)frontCard.RenderTransform;
            var trans = (TranslateTransform)tg.Children[1];

            var dropAnim = new DoubleAnimation(trans.Y + 200, TimeSpan.FromSeconds(0.2)) 
            { 
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } 
            };
            
            dropAnim.Completed += (s, ev) =>
            {
                _cards.RemoveAt(0);
                _cards.Add(frontCard);

                UpdateCardPositions(animate: true);
                
                System.Threading.Tasks.Task.Delay(400).ContinueWith(_ => Dispatcher.Invoke(() => _isAnimating = false));
            };

            trans.BeginAnimation(TranslateTransform.YProperty, dropAnim);
        }
    }
}
