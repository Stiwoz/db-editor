using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Probe.DbEditor.Behaviors;

public static class MiddleClickScrollBehavior
{
    private const double DeadZone = 12;
    private const double MaxPixelsPerTick = 48;
    private const double PixelsPerTickRatio = 0.18;
    private const double HorizontalWheelPixelsPerLine = 48;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(MiddleClickScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "MiddleClickScrollState",
            typeof(MiddleClickScrollState),
            typeof(MiddleClickScrollBehavior));

    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not UIElement uiElement)
        {
            return;
        }

        var state = (MiddleClickScrollState?)uiElement.GetValue(StateProperty);
        if ((bool)e.NewValue)
        {
            state ??= new MiddleClickScrollState(uiElement);
            uiElement.SetValue(StateProperty, state);
            state.Attach();
            return;
        }

        state?.Detach();
        uiElement.ClearValue(StateProperty);
    }

    private static ScrollViewer? FindScrollableViewer(object? originalSource, ScrollDirection direction)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is ScrollBar)
            {
                return null;
            }

            if (current is ScrollViewer viewer && CanScroll(viewer, direction))
            {
                return viewer;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static bool CanScroll(ScrollViewer viewer, ScrollDirection direction)
    {
        return direction switch
        {
            ScrollDirection.Horizontal => viewer.ScrollableWidth > 0,
            _ => viewer.ScrollableHeight > 0 || viewer.ScrollableWidth > 0
        };
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is Visual or Visual3D)
        {
            return VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }

        return LogicalTreeHelper.GetParent(current);
    }

    private static double ComputeVelocity(double distance, bool canScroll)
    {
        if (!canScroll)
        {
            return 0;
        }

        var magnitude = Math.Abs(distance);
        if (magnitude <= DeadZone)
        {
            return 0;
        }

        var velocity = (magnitude - DeadZone) * PixelsPerTickRatio;
        return Math.Sign(distance) * Math.Min(MaxPixelsPerTick, velocity);
    }

    private static bool IsShiftPressed()
    {
        return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
    }

    private static double ComputeHorizontalWheelDistance(int wheelDelta)
    {
        return wheelDelta / (double)Mouse.MouseWheelDeltaForOneLine * HorizontalWheelPixelsPerLine;
    }

    private enum ScrollDirection
    {
        Any,
        Horizontal
    }

    private sealed class MiddleClickScrollState
    {
        private readonly UIElement _root;
        private readonly DispatcherTimer _timer;
        private bool _isAttached;
        private bool _isActive;
        private Cursor? _previousCursor;
        private ScrollViewer? _viewer;
        private Point _origin;
        private double _horizontalVelocity;
        private double _verticalVelocity;

        public MiddleClickScrollState(UIElement root)
        {
            _root = root;
            _timer = new DispatcherTimer(DispatcherPriority.Render, root.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += OnTimerTick;
        }

        public void Attach()
        {
            if (_isAttached)
            {
                return;
            }

            _root.PreviewMouseDown += OnPreviewMouseDown;
            _root.PreviewMouseUp += OnPreviewMouseUp;
            _root.PreviewMouseMove += OnPreviewMouseMove;
            _root.PreviewMouseWheel += OnPreviewMouseWheel;
            _root.PreviewKeyDown += OnPreviewKeyDown;
            _root.LostMouseCapture += OnLostMouseCapture;
            _isAttached = true;
        }

        public void Detach()
        {
            if (!_isAttached)
            {
                return;
            }

            Stop();
            _root.PreviewMouseDown -= OnPreviewMouseDown;
            _root.PreviewMouseUp -= OnPreviewMouseUp;
            _root.PreviewMouseMove -= OnPreviewMouseMove;
            _root.PreviewMouseWheel -= OnPreviewMouseWheel;
            _root.PreviewKeyDown -= OnPreviewKeyDown;
            _root.LostMouseCapture -= OnLostMouseCapture;
            _isAttached = false;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isActive)
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    e.Handled = true;
                }

                return;
            }

            if (e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            var viewer = FindScrollableViewer(e.OriginalSource, ScrollDirection.Any);
            if (viewer is null)
            {
                return;
            }

            Start(viewer, e);
            e.Handled = true;
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle || !_isActive)
            {
                return;
            }

            Stop();
            e.Handled = true;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isActive || _viewer is null)
            {
                return;
            }

            var current = e.GetPosition(_viewer);
            _horizontalVelocity = ComputeVelocity(current.X - _origin.X, _viewer.ScrollableWidth > 0);
            _verticalVelocity = ComputeVelocity(current.Y - _origin.Y, _viewer.ScrollableHeight > 0);
            e.Handled = true;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_isActive)
            {
                e.Handled = true;
                return;
            }

            if (!IsShiftPressed())
            {
                return;
            }

            var viewer = FindScrollableViewer(e.OriginalSource, ScrollDirection.Horizontal);
            if (viewer is null)
            {
                return;
            }

            var offset = Math.Clamp(
                viewer.HorizontalOffset - ComputeHorizontalWheelDistance(e.Delta),
                0,
                viewer.ScrollableWidth);
            viewer.ScrollToHorizontalOffset(offset);
            e.Handled = true;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isActive || e.Key != Key.Escape)
            {
                return;
            }

            Stop();
            e.Handled = true;
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isActive)
            {
                Stop();
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_viewer is null || !_viewer.IsVisible || Mouse.MiddleButton != MouseButtonState.Pressed)
            {
                Stop();
                return;
            }

            if (_horizontalVelocity != 0)
            {
                var offset = Math.Clamp(
                    _viewer.HorizontalOffset + _horizontalVelocity,
                    0,
                    _viewer.ScrollableWidth);
                _viewer.ScrollToHorizontalOffset(offset);
            }

            if (_verticalVelocity != 0)
            {
                var offset = Math.Clamp(
                    _viewer.VerticalOffset + _verticalVelocity,
                    0,
                    _viewer.ScrollableHeight);
                _viewer.ScrollToVerticalOffset(offset);
            }
        }

        private void Start(ScrollViewer viewer, MouseButtonEventArgs e)
        {
            _viewer = viewer;
            _origin = e.GetPosition(viewer);
            _horizontalVelocity = 0;
            _verticalVelocity = 0;
            _previousCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = CursorFor(viewer);

            if (!Mouse.Capture(_root, CaptureMode.SubTree))
            {
                Stop();
                return;
            }

            _isActive = true;
            _timer.Start();
        }

        private void Stop()
        {
            if (!_isActive && _viewer is null)
            {
                return;
            }

            _timer.Stop();
            _isActive = false;
            _viewer = null;
            _horizontalVelocity = 0;
            _verticalVelocity = 0;

            if (Mouse.Captured == _root)
            {
                Mouse.Capture(null);
            }

            Mouse.OverrideCursor = _previousCursor;
            _previousCursor = null;
        }

        private static Cursor CursorFor(ScrollViewer viewer)
        {
            var canScrollHorizontal = viewer.ScrollableWidth > 0;
            var canScrollVertical = viewer.ScrollableHeight > 0;

            return (canScrollHorizontal, canScrollVertical) switch
            {
                (true, true) => Cursors.ScrollAll,
                (true, false) => Cursors.ScrollWE,
                _ => Cursors.ScrollNS
            };
        }
    }
}
