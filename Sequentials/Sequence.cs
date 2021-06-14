using CleanMachine;
using CleanMachine.Behavioral;
using CleanMachine.Interfaces;
using log4net;
using Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Reflection;
using CleanMachine.Behavioral.Behaviors;
using System.Threading.Tasks;

namespace Sequentials
{
    //public class ActivityChainBuilder
    //{
    //    public ActivityChainBuilder(string name, IScheduler behaviorScheduler)
    //    {
    //        Name = name;
    //    }

    //    protected ActivityChain UnderConstruction { get; set; }
    //}

    public class Sequence : StateMachineBase, IDisposable
    {
        protected const string InitialNodeName = "Initial";
        protected const string NoopNodeName = "NoOp";
        protected const string FinalNodeName = "Final";
        protected readonly object _runLock = new object();
        protected readonly CancellationTokenSource _abortTokenSource = new CancellationTokenSource();
        protected readonly Func<bool> _abortCondition;
        protected ActionNode _lastAttachedNode;
        protected ActionNode _detachedNode;
        private readonly TripEventArgs signalArgs = new TripEventArgs();
        private bool _isDisposed;

        public Sequence(string name, IUnityContainer runtimeContainer, ILog logger)
            : base(name, runtimeContainer, logger)
        {
            _autoAdvance = true;
            BuildPhase = Phase.Mutable;
            Result = SequenceResult.Unknown;
            _abortCondition = () => _abortTokenSource.Token.IsCancellationRequested;
        }

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<string> ActionExecuted;

        public event EventHandler Finished;
        public event EventHandler Exited;
        public event EventHandler Aborted;

        public Phase BuildPhase { get; protected set; }

        public ActionNode InitialNode { get; protected set; }

        public ActionNode CurrentNode { get; protected set; }

        public ActionNode FinalNode { get; protected set; }

        public SequenceState State { get; protected set; }

        public SequenceResult Result { get; protected set; }

        internal protected Dictionary<Guid, ActionNode> Nodes { get; } = new Dictionary<Guid, ActionNode>();

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void Run()
        {
            lock (_runLock)
            {
                if (State != SequenceState.Assembling)
                {
                    //TODO: log or throw exception
                    return;
                }

                if (BuildPhase != Phase.Mutable)
                {
                    //TODO: log or throw exception
                    return;
                }

                //CompleteAssembly();

                State = SequenceState.Running;
                SignalAsync(new DataWaypoint(this, nameof(Run)));
            }
        }

        public void Pause()
        {
            //todo: lock on a state object instead?
            lock (_runLock)
            {
                if (State != SequenceState.Running)
                {
                    return;
                }

                State = SequenceState.Paused;
            }
        }

        public void Resume()
        {
            lock (_runLock)
            {
                if (State != SequenceState.Paused)
                {
                    return;
                }

                var resumeArgs = new TripEventArgs(_currentState.VisitIdentifier, new DataWaypoint(this, nameof(Resume)));
                StimulateUnsafe(resumeArgs);
            }
        }

        /// <summary>
        /// Asynchronously signal this machine to stimulate any passive transitions that exit the current state.
        /// If a Behavior scheduler is available, the signal will be sent to it.  Otherwise, Task.Run() is used.
        /// Passive transitions do not have a Trigger.
        /// </summary>
        /// <param name="signalSource"></param>
        /// <returns></returns>
        public override async Task<bool> SignalAsync(DataWaypoint signalSource)
        {
            //if (BehaviorScheduler == null)
            //{
            //    return Task.Run(() => Signal(signalSource)).Result;
            //}
            //else
            //{
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            BehaviorScheduler.Schedule(signalSource, (_, t) =>
            {
                tcs.SetResult(Signal(t));
                return new BlankDisposable();
            });

            return tcs.Task.Result;
            //}
        }

        /// <summary>
        /// Create the Initial and Final nodes.
        /// </summary>
        internal protected void Initialize(string finishName = null, Func<bool> finishCondition = null, IEnumerable<Func<IUnityContainer, TriggerBase>> finishStimuli = null)
        {
            //if (BuildPhase != Phase.Mutable)
            //{
            //    throw new InvalidOperationException("Sequential edit phase is already complete.");
            //}

            if (InitialNode != null)
            {
                throw new InvalidOperationException("An initial node already exists for this activity.");
            }

            if (FinalNode != null)
            {
                throw new InvalidOperationException("A final node already exists for this activity.");
            }

            // Add an initial node that has no action to perform. NOTE: no inbound links here.
            InitialNode = new ActionNode(InitialNodeName, Name, Logger, RuntimeContainer, _abortTokenSource.Token);
            AddNode(InitialNode);
            SetInitialState(InitialNode.Name);

            FinalNode = new ActionNode(FinalNodeName, Name, Logger, RuntimeContainer, _abortTokenSource.Token);
            AddNode(FinalNode);
            FinalNode.SetEntryBehavior(new Behavior(nameof(OnFinished), (c) => OnFinished(c)));
        }

        internal protected void AddNode(ActionNode node)
        {
            Nodes[node.Uid] = node;
            _states.Add(node);
            node.EnteredInternal += HandleStateEnteredInternal;
            //node.Entered += HandleStateEntered;
            //node.Exited += HandleStateExited;
        }

        /// <summary>
        /// Apply common required links from the given node to the Final node.
        /// Every non-final node gets an Abort link, and non-bookend nodes get an Exit link.
        /// </summary>
        /// <param name="node"></param>
        internal protected void SetRequiredLinks(ActionNode node)
        {
            if (node == FinalNode)
            {
                return;
            }

            var abort = node.CreateLinkTo(Name, Stereotypes.Abort.ToString(), FinalNode);
            abort.RuntimeContainer = RuntimeContainer;
            abort.GlobalSynchronizer = _synchronizer;
            abort.Guard = new Constraint(Stereotypes.Abort.ToString(), _abortCondition, Logger);

            //abort.SucceededInternal += HandleLinkSucceededInternal;
            abort.SucceededInternal += HandleTransitionSucceededInternal;
        }

        internal protected void SetRequiredLinks(ActionNode node, IConstraint exitGuard, IEnumerable<Func<IUnityContainer, TriggerBase>> exitStimuli)
        {
            SetRequiredLinks(node);

            // Do not create an exit link if there is no exit guard.
            if (exitGuard == null || node == InitialNode || node == FinalNode)
            {
                return;
            }

            var exit = node.CreateLinkTo(Name, Stereotypes.Exit.ToString(), FinalNode);
            exit.RuntimeContainer = RuntimeContainer;
            exit.GlobalSynchronizer = _synchronizer;
            exit.Guard = exitGuard;

            foreach (var stim in exitStimuli)
            {
                exit.AddTrigger(stim.Invoke(RuntimeContainer));
            }

            //exit.SucceededInternal += HandleLinkSucceededInternal;
            exit.SucceededInternal += HandleTransitionSucceededInternal;
        }

        internal protected void SetTerminalLink(ActionNode node, IConstraint finishGuard, IEnumerable<Func<IUnityContainer, TriggerBase>> finishStimuli)
        {
            if (node == InitialNode || node == FinalNode)
            {
                return;
            }

            var finish = node.CreateLinkTo(Name, Stereotypes.Finish.ToString(), FinalNode);
            finish.RuntimeContainer = RuntimeContainer;
            finish.GlobalSynchronizer = _synchronizer;
            finish.Guard = finishGuard;

            foreach (var stim in finishStimuli)
            {
                finish.AddTrigger(stim.Invoke(RuntimeContainer));
            }

            //finish.SucceededInternal += HandleLinkSucceededInternal;
            finish.SucceededInternal += HandleTransitionSucceededInternal;
        }

        internal protected void SetContinueLink(ActionNode fromNode, ActionNode toNode, IConstraint continueGuard, IEnumerable<Func<IUnityContainer, TriggerBase>> stimuli)
        {
            var @continue = new Link(Name, Stereotypes.Continue.ToString(), Logger);
            @continue.RuntimeContainer = RuntimeContainer;
            @continue.GlobalSynchronizer = _synchronizer;
            @continue.Guard = continueGuard;

            @continue.Connect(fromNode, toNode);

            if (continueGuard != null)
            {
                toNode.Stereotype = $"[{continueGuard.Name}]{toNode.Stereotype}";
            }

            foreach (var stim in stimuli)
            {
                @continue.AddTrigger(stim.Invoke(RuntimeContainer));
            }

            //@continue.SucceededInternal += HandleLinkSucceededInternal;
            @continue.SucceededInternal += HandleTransitionSucceededInternal;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Stimulate the currently enabled passive transitions to attempt to exit the current state.
        /// 
        /// TODO: Change this? Only passive transitions are stimulated because presence of a trigger is
        /// taken to indicate that only the trigger should be able to stimulate the transition.
        /// </summary>
        /// <param name="signalSource"></param>
        /// <returns>True if the signal caused a transition; false otherwise.</returns>
        protected override bool StimulateUnsafe(TripEventArgs tripArgs)
        {
            bool result;
            if (State != SequenceState.Running)
            {
                return false;
            }

            if (result = TryAbort(tripArgs))
            {
                RaiseAborted();
            }
            else if (result = TryExit(tripArgs))
            {
                RaiseExited();
            }
            else if (result = TryFinish(tripArgs))
            {
                RaiseFinished();
            }
            else if (result = TryContinue(tripArgs))
            {

            }
            else
            {
                // CurrentNode is end of the sequence.  Try to finish.
            }

            //TODO LOG IT?
            return result;
        }

        protected bool TryAbort(TripEventArgs tripArgs)
        {
            if (CurrentNode.AbortLink == null)
            {
                return false;
            }

            return CurrentNode.AbortLink.AttemptTraverse(tripArgs);
        }

        protected bool TryExit(TripEventArgs tripArgs)
        {
            if (CurrentNode.ExitLink == null)
            {
                return false;
            }

            return CurrentNode.ExitLink.AttemptTraverse(tripArgs);
        }

        protected bool TryFinish(TripEventArgs tripArgs)
        {
            if (CurrentNode.FinishLink == null)
            {
                return false;
            }

            return CurrentNode.FinishLink.AttemptTraverse(tripArgs);
        }

        protected bool TryContinue(TripEventArgs tripArgs)
        {
            var continuations = CurrentNode.ContinueLinks;
            foreach (var continuation in continuations)
            {
                if (continuation.AttemptTraverse(tripArgs))
                {
                    return true;
                }
            }

            return false;
        }

        protected void OnFinished(IUnityContainer container)
        {
            if (CurrentNode != FinalNode)
            {
                //log
                return;
            }

            //TODO: get transition 
            Link finalLink = null;
            Stereotypes finalReason = finalLink.Stereotype.ToEnum<Stereotypes>();
            switch (finalReason)
            {
                case Stereotypes.Exit:
                    //State = SequenceState.Exited;
                    break;

                case Stereotypes.Abort:
                    //State = SequenceState.Aborted;
                    break;

                case Stereotypes.Continue:
                case Stereotypes.Finish:
                    //State = SequenceState.Finished;
                    break;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Sequence()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        /// <summary>
        /// </summary>
        /// <param name="state"></param>
        /// <param name="args"></param>
        protected override void OnTransitionCompleted(State state, TripEventArgs args)
        {
            if (!state.IsCurrentState)
            {
                //TODO: log
                return;
            }

            if (_abortCondition())
            {
                //log it
                // set aborted state.
                // raise aborted event.
            }
            else
            {
                state.Settle(args);

                // Schedule a new unit of work so that the call stack doesn't grow
                // without bound.
                BehaviorScheduler.Schedule(args, (_, t) => StimulateUnsafe(args));
            }
        }

        //protected override void CreateStates(IEnumerable<string> stateNames)
        //{
        //    throw new NotImplementedException();
        //}

        /// <summary>
        /// Raise the <see cref="ActionExecuted"/> event instead of the <see cref="StateChanged"/> event.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStateChanged(TripEventArgs args)
        {
            CurrentNode = _currentState as ActionNode;

            if (ActionExecuted == null)
            {
                return;
            }

            var state = args.FindLastState();
            try
            {
                Logger.Debug($"{Name}:  raising '{nameof(ActionExecuted)}' event.");
                ActionExecuted?.Invoke(this, state.Name);
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.GetType().Name} during '{nameof(ActionExecuted)}' event from {Name} sequence.", ex);
            }
        }

        private void RaiseFinished()
        {
            try
            {
                Finished?.Invoke(this, null);
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.GetType().Name} during '{nameof(Finished)}' event from {Name} sequence.", ex);
            }
        }

        private void RaiseExited()
        {
            try
            {
                Exited?.Invoke(this, null);
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.GetType().Name} during '{nameof(Exited)}' event from {Name} sequence.", ex);
            }
        }

        private void RaiseAborted()
        {
            try
            {
                Aborted?.Invoke(this, null);
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.GetType().Name} during '{nameof(Aborted)}' event from {Name} sequence.", ex);
            }
        }

        protected override void HandleStateExited(object sender, StateExitedEventArgs args)
        {
            //throw new NotImplementedException();
        }

        protected override void HandleStateEntered(object sender, StateEnteredEventArgs args)
        {
            //throw new NotImplementedException();
        }
    }
}
