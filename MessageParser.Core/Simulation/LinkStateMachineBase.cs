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
        public abstract string LinkTypeName { get; }
        public abstract ITimeService TimeService { get; }
        public abstract IRuleEngine RuleEngine { get; }
        public abstract MessageLinkState CurrentState { get; protected set; }

        public abstract IObservable<LinkStateChangedEventArgs> StateChanged { get; }
        public abstract IObservable<MessageBase> OutboundMessages { get; }
        public abstract IObservable<RuleLogEventArgs> RuleLogs { get; }

        public abstract void Start();
        public abstract void Stop();
        public abstract void Reset(MessageLinkState initialState = MessageLinkState.Connected);
        public abstract void ProcessIncomingMessage(MessageBase message);
        public abstract void EvaluateStateAndRules();
        public abstract void Dispose();
    }

    public abstract class LinkStateMachineBase<TContext> : LinkStateMachineBase where TContext : RuleContext
    {
        private MessageLinkState _currentState = MessageLinkState.Connected;
        protected readonly TContext RuleContext;
        private readonly object _lock = new object();
        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;

        private readonly Subject<LinkStateChangedEventArgs> _stateChangedSubject = new Subject<LinkStateChangedEventArgs>();
        private readonly Subject<MessageBase> _outboundMessagesSubject = new Subject<MessageBase>();
        private readonly Subject<RuleLogEventArgs> _ruleLogsSubject = new Subject<RuleLogEventArgs>();

        public override ITimeService TimeService { get; }
        public override RuleEngine<TContext> RuleEngine { get; }

        public override IObservable<LinkStateChangedEventArgs> StateChanged => _stateChangedSubject;
        public override IObservable<MessageBase> OutboundMessages => _outboundMessagesSubject;
        public override IObservable<RuleLogEventArgs> RuleLogs => _ruleLogsSubject;

        public override MessageLinkState CurrentState
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

        protected LinkStateMachineBase(TContext? context = null, ITimeService? timeService = null, RuleEngine<TContext>? ruleEngine = null)
        {
            TimeService = timeService ?? context?.TimeService ?? new SimulationClock();
            RuleEngine = ruleEngine ?? new RuleEngine<TContext>();
            RuleContext = context ?? CreateRuleContext(TimeService);
            RuleContext.CurrentState = MessageLinkState.Connected;

            TimeService.TimeAdvanced.Subscribe(_ => EvaluateStateAndRules());
        }

        protected virtual TContext CreateRuleContext(ITimeService timeService)
        {
            return (TContext)Activator.CreateInstance(typeof(TContext), timeService)!;
        }

        protected virtual void OnStateTransitioned(MessageLinkState oldState, MessageLinkState newState)
        {
        }

        public override void Start()
        {
            if (_loopTask != null) return;
            _loopCts = new CancellationTokenSource();
            _loopTask = RunLoopAsync(_loopCts.Token);
        }

        public override void Stop()
        {
            _loopCts?.Cancel();
            _loopTask = null;
            _loopCts = null;
        }

        public override void Reset(MessageLinkState initialState = MessageLinkState.Connected)
        {
            lock (_lock)
            {
                _currentState = initialState;
                RuleContext.CurrentState = initialState;
                RuleContext.DegradedStateEnteredTime = null;
            }
            _stateChangedSubject.OnNext(new LinkStateChangedEventArgs(MessageLinkState.Closed, initialState, $"{LinkTypeName} manually reset/reconnected.", TimeService.Now));
        }

        public override void ProcessIncomingMessage(MessageBase message)
        {
            if (message == null) return;

            lock (_lock)
            {
                RuleContext.IncomingMessage = message;
                EvaluateStateAndRules();
                RuleContext.IncomingMessage = null;
            }
        }

        public override void EvaluateStateAndRules()
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

        public override void Dispose()
        {
            Stop();
            _stateChangedSubject.Dispose();
            _outboundMessagesSubject.Dispose();
            _ruleLogsSubject.Dispose();
        }
    }
}
