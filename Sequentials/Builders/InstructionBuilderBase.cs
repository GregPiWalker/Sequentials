using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using CleanMachine;
using CleanMachine.Generic;
using CleanMachine.Behavioral.Behaviors;
using Unity;
using log4net;

namespace Sequentials.Builders
{
    public abstract class InstructionBuilderBase
    {
        protected ILog _logger;
        protected List<Binder> _linkBinders;
        protected Dictionary<string, ActionNode> _namedNodes;
        protected string _finishName;
        protected Func<bool> _finishCondition;
        protected string[] _finishReflexKeys;
        protected string _exitName;
        protected Func<bool> _exitCondition;
        protected string[] _exitReflexKeys;

        /// <summary>
        /// Gets the available Stimuli, which are templates for creating Triggers.
        /// This is virtual so that derived types can have a static set of constructors scoped to them specifically.
        /// </summary>
        protected virtual Dictionary<string, Func<IUnityContainer, TriggerBase>> Stimuli { get; }

        protected Sequence Sequence { get; set; }

        protected ActionNode PreviousSupplier { get; set; }

        protected ActionNode Supplier { get; set; }

        protected ActionNode Consumer { get; set; }

        protected void TakeFrom(InstructionBuilderBase other)
        {
            Sequence = other.Sequence;
            _logger = other._logger;
            _linkBinders = other._linkBinders;
            _namedNodes = other._namedNodes;
            _exitName = other._exitName;
            _exitCondition = other._exitCondition;
            _exitReflexKeys = other._exitReflexKeys;
            _finishName = other._finishName;
            _finishCondition = other._finishCondition;
            _finishReflexKeys = other._finishReflexKeys;

            PreviousSupplier = other.PreviousSupplier;
            Supplier = other.Supplier;
            Consumer = other.Consumer;
        }

        protected virtual void ConfigureStimuli()
        {
            // Intentionally blank
        }

        protected void BeginBuilding(Sequence sequence)
        {
            try
            {
                Sequence = sequence;
                _logger = sequence.Logger;
                Sequence.Edit();

                if (_linkBinders == null)
                {
                    _linkBinders = new List<Binder>();
                }
                else
                {
                    _linkBinders.Clear();
                }

                if (_namedNodes == null)
                {
                    _namedNodes = new Dictionary<string, ActionNode>();
                }
                else
                {
                    _namedNodes.Clear();
                }

                Sequence.Initialize();
                _namedNodes.Add(Sequence.InitialNode.Name, Sequence.InitialNode);
                _namedNodes.Add(Sequence.FinalNode.Name, Sequence.FinalNode);
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex.GetType().Name} during {nameof(BeginBuilding)}:  {ex.Message}", ex);
                throw ex;
            }
        }

        protected void CompleteBuild()
        {
            try
            {
                foreach (var binder in _linkBinders)
                {
                    var from = binder.FromState ?? Sequence.Nodes[binder.FromId];
                    var to = binder.ToState ?? Sequence.Nodes[binder.ToId];

                    var contKeys = binder.ReflexKeys ?? new string[] { };
                    var contStimuli = from key in contKeys
                                      where Stimuli.ContainsKey(key)
                                      select Stimuli[key];
                    Sequence.SetContinueLink(from as ActionNode, to as ActionNode, binder.Guard, contStimuli);
                }

                var exitKeys = _exitReflexKeys ?? new string[] { };
                var exitStimuli = from key in exitKeys
                                  where Stimuli.ContainsKey(key)
                                  select Stimuli[key];
                foreach (var node in Sequence.Nodes.Values)
                {
                    // TODO: Should No-Op nodes get abort/exit links?
                    Sequence.SetRequiredLinks(node, new Constraint(_exitName, _exitCondition, Sequence.Logger), exitStimuli);
                }

                var finishKeys = _finishReflexKeys ?? new string[] { };
                var finishStimuli = from key in finishKeys
                                    where Stimuli.ContainsKey(key)
                                    select Stimuli[key];
                // The last time Consumer was set should be the last node before the FinalNode.
                Sequence.SetTerminalLink(Consumer, new Constraint(_finishName, _finishCondition, Sequence.Logger), finishStimuli);

                Sequence.CompleteEdit();
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex.GetType().Name} during {nameof(CompleteBuild)}:  {ex.Message}", ex);
                throw ex;
            }
        }

        protected InstructionBuilderBase AddStart(string actionName, Action<IUnityContainer> action)
        {
            Consumer = Sequence.InitialNode;
            var start = AppendActionNode(actionName, action);
            Sequence.SetInitialState(start.Name);

            return this;
        }

        protected InstructionBuilderBase AddStartWhen(string whenName, Func<bool> whenCondition, params string[] reflexKeys)
        {
            Consumer = Sequence.InitialNode;
            var start = AppendNoOpNode("When", whenName, whenCondition, reflexKeys);
            Sequence.SetInitialState(start.Name);

            return this;
        }

        protected InstructionBuilderBase AddDo(string actionName, Action<IUnityContainer> action)
        {
            AppendActionNode(actionName, action);

            return this;
        }

        protected InstructionBuilderBase AddWhen(string whenName, Func<bool> whenCondition, params string[] reflexKeys)
        {
            // Add the consumer no-op node and a link to it from the previous node.
            AppendNoOpNode("When", whenName, whenCondition, reflexKeys);

            return this;
        }

        protected InstructionBuilderBase AddOrWhen(string conditionName, Func<bool> condition, params string[] reflexKeys)
        {
            var previous = PreviousSupplier;
            // Add the consumer no-op node and a link to it from the previous node.
            EstablishLink(Supplier, Consumer, conditionName, condition, reflexKeys);

            return this;
        }

        protected InstructionBuilderBase AddJumpIf(string branchDestName, string ifName, Func<bool> ifCondition, params string[] reflexKeys)
        {
            // Add the branch node and a link to it from the previous node.
            AppendNoOpNode("Branch");

            // Add the branching link to a branch target.
            var destination = _namedNodes[branchDestName];
            EstablishLink(Supplier, destination, ifName, ifCondition, reflexKeys);

            // Add a no-op node to consume the conditional continuation link.
            // The opposite condition needs the same triggers.
            AppendNoOpNode("NoOp", "Not " + ifName, () => !ifCondition(), reflexKeys);

            return this;
        }

        protected InstructionBuilderBase AddIfThen(string ifName, Func<bool> ifCondition, string thenName, Action<IUnityContainer> thenBehavior, params string[] reflexKeys)
        {
            AppendConditionalActionNode(thenName, thenBehavior, ifName, ifCondition, reflexKeys);

            // Add the no-op node to tie up both links.
            AppendNoOpNode("NoOp");

            // Add the by-pass link to skip over the THEN action.
            EstablishLink(PreviousSupplier, Consumer, "Not " + ifName, () => !ifCondition(), reflexKeys);

            return this;
        }

        protected InstructionBuilderBase AddIfThenElse(string ifName, Func<bool> ifCondition, string thenName, Action<IUnityContainer> thenBehavior, string elseName, Action<IUnityContainer> elseBehavior, params string[] reflexKeys)
        {
            AppendConditionalActionNode(thenName, thenBehavior, ifName, ifCondition, reflexKeys);

            // Add the no-op node to tie up both links.
            var noOp = AppendNoOpNode("NoOp");

            // Restore references in order to add another linked node from the same supplier.
            Consumer = PreviousSupplier;
            AppendConditionalActionNode(elseName, elseBehavior, "Not " + ifName, () => !ifCondition(), reflexKeys);

            // Now add a link from the ELSE node to the no-op terminal node.
            EstablishLink(Consumer, noOp);

            // Finally, fix the reference.
            Consumer = noOp;

            return this;
        }

        /// <summary>
        /// Finish at any time when the supplied finish condition is met.  If no finish condition is given,
        /// the sequence will only finish at its end.
        /// </summary>
        /// <param name="finishName"></param>
        /// <param name="finishCondition"></param>
        /// <param name="reflexKeys"></param>
        /// <returns></returns>
        protected Sequence AddFinish(string finishName = null, Func<bool> finishCondition = null, params string[] reflexKeys)
        {
            _finishName = finishName;
            _finishCondition = finishCondition;
            _finishReflexKeys = reflexKeys;
            return Sequence;
        }

        internal ActionNode CreateNode(string nodeName, Action<IUnityContainer> doBehavior = null)
        {
            //TODO: real cancellation token
            var node = new ActionNode(nodeName, Sequence.Name, _logger, Sequence.RuntimeContainer, new CancellationTokenSource().Token);
            if (doBehavior != null)
            {
                node.AddDoBehavior(new Behavior(nodeName, doBehavior));
            }

            Sequence.AddNode(node);
            return node;
        }

        //internal Link CreateLink(IConstraint constraint = null)
        //{
        //    var link = new Link(Sequence.Name, Stereotypes.Continue.ToString(), _logger);
        //    link.Guard = constraint;
        //    link.RuntimeContainer = Sequence.RuntimeContainer;

        //    //link.GlobalSynchronizer = Sequence._synchronizer;

        //    return link;
        //}

        internal ActionNode AppendActionNode(string actionName, Action<IUnityContainer> action)
        {
            PreviousSupplier = Supplier;
            Supplier = Consumer;
            Consumer = CreateNode(actionName, action);
            _namedNodes.Add(Consumer.Name, Consumer);

            EstablishLink(Supplier, Consumer);

            return Consumer;
        }

        internal ActionNode AppendConditionalActionNode(string actionName, Action<IUnityContainer> action, string conditionName, Func<bool> condition, params string[] reflexKeys)
        {
            PreviousSupplier = Supplier;
            Supplier = Consumer;
            Consumer = CreateNode(actionName, action);
            _namedNodes.Add(Consumer.Name, Consumer);

            EstablishLink(Supplier, Consumer, conditionName, condition, reflexKeys);

            return Consumer;
        }


        internal ActionNode AppendNoOpNode(string nodeName, string conditionName = null, Func<bool> condition = null, params string[] reflexKeys)
        {
            PreviousSupplier = Supplier;
            Supplier = Consumer;
            Consumer = CreateNode(nodeName);

            EstablishLink(Supplier, Consumer, conditionName, condition, reflexKeys);

            return Consumer;
        }

        internal void EstablishLink(State fromState, State toState, string conditionName = null, Func<bool> condition = null, params string[] reflexKeys)
        {
            var binder = new Binder() { FromState = fromState, ToState = toState, ReflexKeys = reflexKeys };
            if (condition != null)
            {
                binder.Guard = new Constraint(conditionName, condition, _logger);
            }

            _linkBinders.Add(binder);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TEventArgs">The Type of the EventArgs to build the Trigger for.</typeparam>
        /// <param name="stimuli"></param>
        /// <param name="key"></param>
        /// <param name="evSource"></param>
        /// <param name="evName"></param>
        /// <param name="filter">a Func that will be given to an IConstraint in order to implement a filter for the Trigger.</param>
        /// <param name="filterName">A string name for the IConstraint used to implement the given filter.</param>
        protected static void AddStimulus<TSource, TEventArgs>(Dictionary<string, Func<IUnityContainer, TriggerBase>> stimuli, string key, TSource evSource, string evName, Func<TEventArgs, bool> filter = null, string filterName = null) //where TEventArgs : EventArgs
        {
            if (stimuli.ContainsKey(key))
            {
                return;
            }

            if (filter == null)
            {
                stimuli[key] = (c) =>
                {
                    IScheduler scheduler = c.TryGetInstance<IScheduler>(StateMachineBase.BehaviorSchedulerKey);
                    ILog logger = c.TryGetTypeRegistration<ILog>();
                    return new Trigger<TSource, TEventArgs>(evSource, evName, scheduler, logger);
                };
            }
            else
            {
                stimuli[key] = (c) =>
                {
                    IScheduler scheduler = c.TryGetInstance<IScheduler>(StateMachineBase.BehaviorSchedulerKey);
                    ILog logger = c.TryGetTypeRegistration<ILog>();
                    return new Trigger<TSource, TEventArgs>(evSource, evName, new Constraint<TEventArgs>(filterName, filter, logger), scheduler, logger);
                };
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TDelegate">The Type of the EventHandler delegate to build the Trigger for.</typeparam>
        /// <typeparam name="TEventArgs">The Type of the EventArgs to build the Trigger for.</typeparam>
        /// <param name="stimuli"></param>
        /// <param name="key"></param>
        /// <param name="evSource"></param>
        /// <param name="evName"></param>
        /// <param name="filter">a Func that will be given to an IConstraint in order to implement a filter for the Trigger.</param>
        /// <param name="filterName">A string name for the IConstraint used to implement the given filter.</param>
        protected static void AddDelegateStimulus<TSource, TDelegate, TEventArgs>(Dictionary<string, Func<IUnityContainer, TriggerBase>> stimuli, string key, TSource evSource, string evName, Func<TEventArgs, bool> filter = null, string filterName = null) //where TEventArgs : EventArgs
        {
            if (stimuli.ContainsKey(key))
            {
                return;
            }

            if (filter == null)
            {
                stimuli[key] = (c) =>
                {
                    IScheduler scheduler = c.TryGetInstance<IScheduler>(StateMachineBase.BehaviorSchedulerKey);
                    ILog logger = c.TryGetTypeRegistration<ILog>();
                    return new DelegateTrigger<TSource, TDelegate, TEventArgs>(evSource, evName, scheduler, logger);
                };
            }
            else
            {
                stimuli[key] = (c) =>
                {
                    IScheduler scheduler = c.TryGetInstance<IScheduler>(StateMachineBase.BehaviorSchedulerKey);
                    ILog logger = c.TryGetTypeRegistration<ILog>();
                    return new DelegateTrigger<TSource, TDelegate, TEventArgs>(evSource, evName, new Constraint<TEventArgs>(filterName, filter, logger), scheduler, logger);
                };
            }
        }
    }
}
