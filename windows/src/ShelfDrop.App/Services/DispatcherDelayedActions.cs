using System;
using System.Windows.Threading;
using ShelfDrop.Core;

namespace ShelfDrop.App.Services
{
    /// <summary>Runs something once, a moment later, on the UI thread.</summary>
    internal sealed class DispatcherDelayedActions : IDelayedActions
    {
        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = delay };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                action();
            };
            timer.Start();
            return new Cancel(timer);
        }

        private sealed class Cancel : IDisposable
        {
            private readonly DispatcherTimer _timer;

            public Cancel(DispatcherTimer timer) => _timer = timer;

            public void Dispose() => _timer.Stop();
        }
    }
}
