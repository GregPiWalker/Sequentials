using System.Reactive.Concurrency;
using System.Threading;
using Unity.Lifetime;
using Unity;
using log4net;
using CleanMachine;

namespace Sequentials
{
    public static class SequenceFactory
    {
        /// <summary>
        /// Create a fully asynchronous Sequence.  A scheduler with a dedicated background thread is instantiated for
        /// internal transitions.  Another scheduler with a dedicated background thread is instantiated for running
        /// the following behaviors: ENTRY, DO, EXIT, EFFECT.  Both schedulers serialize their workflow, but will
        /// operate asynchronously with respect to each other, as well as with respect to incoming trigger invocations.
        /// </summary>
        /// <param name="sequenceName">A name that identifies the Sequence.</param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static Sequence CreateAsync(string sequenceName, ILog logger)
        {
            IUnityContainer container = new UnityContainer();
            var triggerScheduler = new EventLoopScheduler((a) => { return new Thread(a) { Name = $"{sequenceName} Trigger Scheduler", IsBackground = true }; });
            container.RegisterInstance(typeof(IScheduler), StateMachineBase.TriggerSchedulerKey, triggerScheduler, new ContainerControlledLifetimeManager());
            var behaviorScheduler = new EventLoopScheduler((a) => { return new Thread(a) { Name = $"{sequenceName} Action Scheduler", IsBackground = true }; });
            container.RegisterInstance(typeof(IScheduler), StateMachineBase.BehaviorSchedulerKey, behaviorScheduler, new ContainerControlledLifetimeManager());
            var machine = new Sequence(sequenceName, container, logger);
            return machine;
        }

        ///// <summary>
        ///// Create a partially asynchronous Sequence.  A scheduler with a dedicated background thread is instantiated for
        ///// internal triggers and signals.  UML behaviors (ENTRY, DO, EXIT, EFFECT) are executed synchronously on the same internal thread.
        ///// The scheduler serializes its workflow, but will operate asynchronously with respect to incoming trigger invocations.
        ///// </summary>
        ///// <param name="sequenceName">A name that identifies the Sequence.</param>
        ///// <param name="logger"></param>
        ///// <param name="externalSynchronizer">An optional object to synchronize the state machine's internal triggers and signals with other external threaded work.
        ///// If none is supplied, an internal object is used.</param>
        ///// <returns></returns>
        //public static Sequence CreateTriggerAsync(string sequenceName, ILog logger, object externalSynchronizer = null)
        //{
        //    IUnityContainer container = new UnityContainer();
        //    var triggerScheduler = new EventLoopScheduler((a) => { return new Thread(a) { Name = $"{sequenceName} Trigger Scheduler", IsBackground = true }; });
        //    container.RegisterInstance(typeof(IScheduler), StateMachineBase.TriggerSchedulerKey, triggerScheduler, new ContainerControlledLifetimeManager());
        //    if (externalSynchronizer != null)
        //    {
        //        container.RegisterInstance(StateMachineBase.GlobalSynchronizerKey, externalSynchronizer);
        //    }

        //    var machine = new Sequence(sequenceName, container, logger);
        //    return machine;
        //}

        /// <summary>
        /// Create a partially asynchronous Sequence.  A scheduler with a dedicated background thread is instantiated for
        /// UML behaviors (ENTRY, DO, EXIT, EFFECT).  Internal triggers and signals are executed synchronously on the same thread
        /// as the triggers' event handlers.
        /// The scheduler serializes its workflow, but will operate asynchronously with respect to incoming trigger invocations.
        /// </summary>
        /// <param name="sequenceName">A name that identifies the Sequence.</param>
        /// <param name="logger"></param>
        /// <param name="externalSynchronizer">An optional object to synchronize the state machine's internal triggers and signals with other external threaded work.
        /// If none is supplied, an internal object is used.</param>
        /// <returns></returns>
        public static Sequence CreateActionAsync(string sequenceName, ILog logger, object externalSynchronizer = null)
        {
            IUnityContainer container = new UnityContainer();
            var behaviorScheduler = new EventLoopScheduler((a) => { return new Thread(a) { Name = $"{sequenceName} Action Scheduler", IsBackground = true }; });
            container.RegisterInstance(typeof(IScheduler), StateMachineBase.BehaviorSchedulerKey, behaviorScheduler, new ContainerControlledLifetimeManager());
            if (externalSynchronizer != null)
            {
                container.RegisterInstance(StateMachineBase.GlobalSynchronizerKey, externalSynchronizer);
            }

            var machine = new Sequence(sequenceName, container, logger);
            return machine;
        }
    }
}
