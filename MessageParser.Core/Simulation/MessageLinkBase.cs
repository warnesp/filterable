using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using MessageParser.Core.Messages;

namespace MessageParser.Core.Simulation
{
    public abstract class MessageLinkBase : IMessageLink
    {
        private CancellationTokenSource? _cts;
        private Task? _runTask;

        protected readonly Subject<string> RawMessageSubject = new Subject<string>();
        protected readonly Subject<MessageBase> MessageSubject = new Subject<MessageBase>();

        public abstract string LinkId { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public LinkStateMachineBase StateMachine { get; }
        public ITimeService TimeService => StateMachine.TimeService;

        public IObservable<string> RawMessageStream => RawMessageSubject;
        public IObservable<MessageBase> MessageStream => MessageSubject;

        public bool IsReceivingHeartbeats { get; set; } = true;

        protected MessageLinkBase(LinkStateMachineBase stateMachine)
        {
            StateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            StateMachine.OutboundMessages.Subscribe(OnRuleOutboundMessageGenerated);
        }

        private void OnRuleOutboundMessageGenerated(MessageBase message)
        {
            MessageSubject.OnNext(message);
            string formattedRaw = FormatMessageToRaw(message);
            if (!string.IsNullOrEmpty(formattedRaw))
            {
                RawMessageSubject.OnNext(formattedRaw);
            }
        }

        public virtual void Start()
        {
            if (_runTask != null) return;

            StateMachine.Start();
            _cts = new CancellationTokenSource();
            _runTask = RunSimulationLoopAsync(_cts.Token);
        }

        public virtual void Stop()
        {
            StateMachine.Stop();
            if (_runTask == null) return;

            _cts?.Cancel();
            _runTask = null;
            _cts = null;
        }

        public virtual void Reset()
        {
            StateMachine.Reset(MessageLinkState.Connected);
            IsReceivingHeartbeats = true;
        }

        private async Task RunSimulationLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await TimeService.DelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

                if (StateMachine.CurrentState != MessageLinkState.Closed)
                {
                    string rawMessage = GenerateRandomMessage();
                    if (!string.IsNullOrEmpty(rawMessage))
                    {
                        RawMessageSubject.OnNext(rawMessage);
                    }
                }
            }
        }

        public abstract string GenerateRandomMessage();
        protected abstract string FormatMessageToRaw(MessageBase msg);

        public virtual void Dispose()
        {
            Stop();
            StateMachine.Dispose();
            RawMessageSubject.Dispose();
            MessageSubject.Dispose();
        }
    }
}
