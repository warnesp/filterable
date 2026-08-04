using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using MessageParser.Core.Messages;
using MessageParser.Core.Rules;

namespace MessageParser.Core.Simulation
{
    public class LinkStateChangedEventArgs
    {
        public MessageLinkState OldState { get; }
        public MessageLinkState NewState { get; }
        public string Reason { get; }
        public DateTime Timestamp { get; }

        public LinkStateChangedEventArgs(MessageLinkState oldState, MessageLinkState newState, string reason, DateTime timestamp)
        {
            OldState = oldState;
            NewState = newState;
            Reason = reason;
            Timestamp = timestamp;
        }
    }

    public class RuleLogEventArgs
    {
        public string RuleId { get; }
        public string RuleName { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public RuleLogEventArgs(string ruleId, string ruleName, string message, DateTime timestamp)
        {
            RuleId = ruleId;
            RuleName = ruleName;
            Message = message;
            Timestamp = timestamp;
        }
    }

    public class LinkStateMachine
    {
        private MessageLinkState _currentState = MessageLinkState.Connected;
        private readonly RuleContext _ruleContext;
        private readonly object _lock = new object();
        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;

        private readonly Subject<LinkStateChangedEventArgs> _stateChangedSubject = new Subject<LinkStateChangedEventArgs>();
        private readonly Subject<MessageBase> _outboundMessagesSubject = new Subject<MessageBase>();
        private readonly Subject<RuleLogEventArgs> _ruleLogsSubject = new Subject<RuleLogEventArgs>();

        public ITimeService TimeService { get; }
        public RuleEngine RuleEngine { get; }

        // Reactive IObservable streams
        public IObservable<LinkStateChangedEventArgs> StateChanged => _stateChangedSubject;
        public IObservable<MessageBase> OutboundMessages => _outboundMessagesSubject;
        public IObservable<RuleLogEventArgs> RuleLogs => _ruleLogsSubject;

        public MessageLinkState CurrentState
        {
            get
            {
                lock (_lock) return _currentState;
            }
            private set
            {
                lock (_lock)
                {
                    if (_currentState != value)
                    {
                        var oldState = _currentState;
                        _currentState = value;
                        _ruleContext.CurrentState = value;

                        if (value == MessageLinkState.Degraded)
                        {
                            _ruleContext.DegradedStateEnteredTime = TimeService.Now;
                        }
                    }
                }
            }
        }

        public LinkStateMachine(ITimeService? timeService = null, RuleEngine? ruleEngine = null)
        {
            TimeService = timeService ?? new SimulationClock();
            RuleEngine = ruleEngine ?? new RuleEngine();
            _ruleContext = new RuleContext(TimeService)
            {
                CurrentState = MessageLinkState.Connected,
                LastHeartbeatReceivedTime = TimeService.Now,
                LastHeartbeatSentTime = TimeService.Now,
                LastAirTrackSentTime = TimeService.Now
            };

            // Hook clock manual advances stream to immediately trigger evaluation tick
            TimeService.TimeAdvanced.Subscribe(_ => EvaluateStateAndRules());
        }

        public void Start()
        {
            if (_loopTask != null) return;
            _loopCts = new CancellationTokenSource();
            _loopTask = RunLoopAsync(_loopCts.Token);
        }

        public void Stop()
        {
            _loopCts?.Cancel();
            _loopTask = null;
            _loopCts = null;
        }

        public void Reset(MessageLinkState initialState = MessageLinkState.Connected)
        {
            lock (_lock)
            {
                _currentState = initialState;
                _ruleContext.CurrentState = initialState;
                _ruleContext.LastHeartbeatReceivedTime = TimeService.Now;
                _ruleContext.DegradedStateEnteredTime = null;
                _ruleContext.LastHeartbeatSentTime = TimeService.Now;
                _ruleContext.LastAirTrackSentTime = TimeService.Now;
                _ruleContext.CurrentFuelLevel = 100.0;
            }
            _stateChangedSubject.OnNext(new LinkStateChangedEventArgs(MessageLinkState.Closed, initialState, "Link manually reset/reconnected.", TimeService.Now));
        }

        public void ProcessIncomingMessage(MessageBase message)
        {
            if (message == null) return;

            lock (_lock)
            {
                _ruleContext.IncomingMessage = message;
                EvaluateStateAndRules();
                _ruleContext.IncomingMessage = null;
            }
        }

        public void SetFuelLevel(double fuelPercent)
        {
            lock (_lock)
            {
                _ruleContext.CurrentFuelLevel = fuelPercent;
                EvaluateStateAndRules();
            }
        }

        public void EvaluateStateAndRules()
        {
            lock (_lock)
            {
                _ruleContext.CurrentState = _currentState;
                var results = RuleEngine.EvaluateAll(_ruleContext);

                foreach (var res in results)
                {
                    if (res.ProposedState.HasValue && res.ProposedState.Value != _currentState)
                    {
                        var oldState = _currentState;
                        _currentState = res.ProposedState.Value;
                        _ruleContext.CurrentState = _currentState;

                        if (_currentState == MessageLinkState.Degraded)
                        {
                            _ruleContext.DegradedStateEnteredTime = TimeService.Now;
                        }

                        string reason = res.LogMessage ?? $"Transitioned to {_currentState}";
                        _stateChangedSubject.OnNext(new LinkStateChangedEventArgs(oldState, _currentState, reason, TimeService.Now));
                    }

                    if (res.MessageToSend != null)
                    {
                        _outboundMessagesSubject.OnNext(res.MessageToSend);
                    }

                    if (!string.IsNullOrEmpty(res.LogMessage))
                    {
                        _ruleLogsSubject.OnNext(new RuleLogEventArgs("RULE", "Rules Engine", res.LogMessage, TimeService.Now));
                    }
                }
            }
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                EvaluateStateAndRules();

                // Evaluate every 100ms simulated or real interval
                await TimeService.DelayAsync(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
