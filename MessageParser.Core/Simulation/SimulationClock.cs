using System.Diagnostics;
using System.Reactive.Subjects;

namespace MessageParser.Core.Simulation
{
    public class SimulationClock : ITimeService
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private DateTime _startWallTime;
        private TimeSpan _manualOffset = TimeSpan.Zero;
        private double _timeScale = 1.0;
        private readonly object _lock = new object();
        private readonly Subject<DateTime> _timeAdvancedSubject = new Subject<DateTime>();

        public IObservable<DateTime> TimeAdvanced => _timeAdvancedSubject;

        public SimulationClock(DateTime? initialSimulatedTime = null)
        {
            _startWallTime = initialSimulatedTime ?? DateTime.UtcNow;
            _stopwatch.Start();
        }

        public DateTime Now
        {
            get
            {
                lock (_lock)
                {
                    double elapsedScaledMs = _stopwatch.ElapsedMilliseconds * _timeScale;
                    return _startWallTime.AddMilliseconds(elapsedScaledMs).Add(_manualOffset);
                }
            }
        }

        public double TimeScale
        {
            get
            {
                lock (_lock) return _timeScale;
            }
            set
            {
                lock (_lock)
                {
                    // Re-anchor start time so scale change applies seamlessly from now
                    var currentSimNow = Now;
                    _startWallTime = currentSimNow;
                    _manualOffset = TimeSpan.Zero;
                    _stopwatch.Restart();
                    _timeScale = Math.Max(0.0, value);
                }
            }
        }

        public void AdvanceTime(TimeSpan delta)
        {
            DateTime newTime;
            lock (_lock)
            {
                _manualOffset += delta;
                newTime = Now;
            }
            _timeAdvancedSubject.OnNext(newTime);
        }

        public async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            if (duration <= TimeSpan.Zero) return;

            DateTime targetTime = Now.Add(duration);

            while (!cancellationToken.IsCancellationRequested && Now < targetTime)
            {
                double scale;
                lock (_lock)
                {
                    scale = _timeScale;
                }

                if (scale <= 0.0001)
                {
                    // Paused - check periodically for manual advances or scale changes
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    TimeSpan remaining = targetTime - Now;
                    if (remaining <= TimeSpan.Zero) break;

                    int realWaitMs = (int)Math.Max(1, Math.Min(100, remaining.TotalMilliseconds / scale));
                    await Task.Delay(realWaitMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
