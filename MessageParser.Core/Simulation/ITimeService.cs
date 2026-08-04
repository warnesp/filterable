using System;
using System.Threading;
using System.Threading.Tasks;

namespace MessageParser.Core.Simulation
{
    public interface ITimeService
    {
        /// <summary>
        /// Current simulated time.
        /// </summary>
        DateTime Now { get; }

        /// <summary>
        /// Speed multiplier (1.0 = real-time, 2.0 = 2x speed, 0.0 = paused).
        /// </summary>
        double TimeScale { get; set; }

        /// <summary>
        /// Advances simulated time manually by the specified duration.
        /// </summary>
        void AdvanceTime(TimeSpan delta);

        /// <summary>
        /// Asynchronously waits for simulated time duration, accounting for TimeScale.
        /// </summary>
        Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Observable stream emitted whenever simulated time changes/advances.
        /// </summary>
        IObservable<DateTime> TimeAdvanced { get; }
    }
}
