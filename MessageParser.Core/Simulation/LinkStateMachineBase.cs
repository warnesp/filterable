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

    public abstract class LinkStateMachineBase : IDisposable
    {
        private MessageLinkState _currentState = MessageLinkState.Connected;
        protected readonly RuleContext RuleContext;
        private readonly object _lock = new object();
        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;

        private readonly Subject<LinkStateChangedEventArgs> _stateChangedSubject = new Subject<LinkStateChangedEventArgs>();
        private readonly Subject<MessageBase> _outboundMessagesSubject = new Subject<MessageBase>();
        private readonly Subject<RuleLogEventArgs> _ruleLogsSubject = new Subject<RuleLogEventArgs>();

        public abstract string LinkTypeName { get; }
        public ITimeService TimeService { get; }
        public RuleEngine RuleEngine { get; }

        // Reactive IObservable streams shared by all link state machines
        public IObservable<LinkStateChangedEventArgs> StateChanged => _stateChangedSubject;
        public IObservable<MessageBase> OutboundMessages => _outboundMessagesSubject;
        public IObservable<RuleLogEventArgs> RuleLogs => _ruleLogsSubject;

        public MessageLinkState CurrentState
        {
            get
            {
                lock (_lock) return _currentState;
            }
            protected set
            {
                lock (_lock)
                {
                    if (_currentState != value)
                    {
                        var oldState = _currentState;
                        _currentState = value;
                        RuleContext.CurrentState = value;

                        if (value == MessageLinkState.Degraded)
                        {
                            RuleContext.DegradedStateEnteredTime = TimeService.Now;
                        }

                        OnStateTransitioned(oldState, value);
                    }
                }
            }
        }

        protected LinkStateMachineBase(ITimeService? timeService = null, RuleEngine? ruleEngine = null)
        {
            TimeService = timeService ?? new SimulationClock();
            RuleEngine = ruleEngine ?? new RuleEngine();
            RuleContext = new RuleContext(TimeService)
            {
                CurrentState = MessageLinkState.Connected,
                LastHeartbeatReceivedTime = TimeService.Now,
                LastHeartbeatSentTime = TimeService.Now,
                LastAirTrackSentTime = TimeService.Now
            };

            // Hook clock manual advances stream to immediately trigger evaluation tick
            TimeService.TimeAdvanced.Subscribe(_ => EvaluateStateAndRules());
        }

        protected virtual void OnStateTransitioned(MessageLinkState oldState, MessageLinkState newState)
        {
        }

        public virtual void Start()
        {
            if (_loopTask != null) return;
            _loopCts = new CancellationTokenSource();
            _loopTask = RunLoopAsync(_loopCts.Token);
        }

        public virtual void Stop()
        {
            _loopCts?.Cancel();
            _loopTask = null;
            _loopCts = null;
        }

        public virtual void Reset(MessageLinkState initialState = MessageLinkState.Connected)
        {
            lock (_lock)
            {
                _currentState = initialState;
                RuleContext.CurrentState = initialState;
                RuleContext.LastHeartbeatReceivedTime = TimeService.Now;
                RuleContext.DegradedStateEnteredTime = null;
                RuleContext.LastHeartbeatSentTime = TimeService.Now;
                RuleContext.LastAirTrackSentTime = TimeService.Now;
            }
            _stateChangedSubject.OnNext(new LinkStateChangedEventArgs(MessageLinkState.Closed, initialState, $"{LinkTypeName} manually reset/reconnected.", TimeService.Now));
        }

        public virtual void ProcessIncomingMessage(MessageBase message)
        {
            if (message == null) return;

            lock (_lock)
            {
                RuleContext.IncomingMessage = message;
                EvaluateStateAndRules();
                RuleContext.IncomingMessage = null;
            }
        }

        public virtual void EvaluateStateAndRules()
        {
            lock (_lock)
            {
                RuleContext.CurrentState = _currentState;
                var results = RuleEngine.EvaluateAll(RuleContext);

                foreach (var res in results)
                {
                    if (res.ProposedState.HasValue && res.ProposedState.Value != _currentState)
                    {
                        var oldState = _currentState;
                        _currentState = res.ProposedState.Value;
                        RuleContext.CurrentState = _currentState;

                        if (_currentState == MessageLinkState.Degraded)
                        {
                            RuleContext.DegradedStateEnteredTime = TimeService.Now;
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
                        _ruleLogsSubject.OnNext(new RuleLogEventArgs("RULE", RuleTypeNameForLog(res), res.LogMessage, TimeService.Now));
                    }
                }
            }
        }

        protected virtual string RuleTypeNameForLog(RuleExecutionResult res) => "Rules Engine";

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                EvaluateStateAndRules();

                // Evaluate every 100ms simulated or real interval
                await TimeService.DelayAsync(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }

        public virtual void Dispose()
        {
            Stop();
            _stateChangedSubject.Dispose();
            _outboundMessagesSubject.Dispose();
            _ruleLogsSubject.Dispose();
        }
    }
}
