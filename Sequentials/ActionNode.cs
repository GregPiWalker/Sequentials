using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using log4net;
using CleanMachine;
using CleanMachine.Interfaces;
using CleanMachine.Behavioral;
using Unity;

namespace Sequentials
{
    public class ActionNode : BehavioralState
    {
        protected string _context;
        private static readonly LinkComparer Comparer = new LinkComparer();

        public ActionNode(string name, string context, ILog logger, IUnityContainer runtimeContainer, CancellationToken abortToken)
            : base(name, runtimeContainer, logger)
        {
            _context = context;
            AbortToken = abortToken;
            ValidateTrips = false;
            Uid = Guid.NewGuid();
        }

        public Guid Uid { get; }

        public IConstraint ExitConstraint { get; protected set; }

        public CancellationToken AbortToken { get; }

        /// <summary>
        /// Gets the Link for clean exit from this node to the final node.
        /// </summary>
        public Link ExitLink { get; protected set; }

        /// <summary>
        /// Gets the Link for abortion from this node to the final node.
        /// </summary>
        public Link AbortLink { get; protected set; }

        /// <summary>
        /// Gets a collection of CONTINUE Links from this node to the next activity node.
        /// </summary>
        public IEnumerable<Link> ContinueLinks => _outboundTransitions.Where(t => t.HasStereotype(Stereotypes.Continue)).Cast<Link>();

        public IOrderedEnumerable<Link> PrioritizedLinks => _outboundTransitions.Cast<Link>().OrderBy(t => t);

        /// <summary>
        /// Determine whether this node can be exited on the given transition.
        /// Exit and Abort transitions bypass this node's EXIT condition; all
        /// other nodes are restricted from exit by the EXIT condition.
        /// </summary>
        /// <param name="exitOn"></param>
        /// <returns></returns>
        public override bool CanExit(Transition exitOn)
        {
            var canExit = base.CanExit(exitOn);

            if (canExit)
            {
                if (exitOn == AbortLink && AbortToken.IsCancellationRequested)
                {
                    canExit = true;
                }
                else if (exitOn == ExitLink)
                {
                    // TODO: use a constraint
                    canExit = true;
                }
                else
                {
                    canExit = ExitConstraint?.IsTrue() ?? false;
                }
            }

            return canExit;
        }

        internal virtual Link CreateLinkTo(string context, string stereotype, ActionNode consumer)
        {
            var link = new Link(context, stereotype, consumer, _logger);
            if (Stereotypes.Abort.ToString().Equals(stereotype))
            {
                AbortLink = link;
            }
            else if (Stereotypes.Exit.ToString().Equals(stereotype))
            {
                ExitLink = link;
            }

            AddTransition(link);

            return link;
        }

        protected override void Settle(TripEventArgs tripArgs)
        {
            if (AbortToken.IsCancellationRequested)
            {
                return;
            }

            base.Settle(tripArgs);
        }
    }

    class LinkComparer : IComparer<Link>
    {
        /// <summary>
        /// Sort Links in order of their Stereotype.
        /// ( Abort > Exit > Finish > Continue )
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int Compare(Link x, Link y)
        {
            if (x.Stereotype.Equals(y.Stereotype))
            {
                return 0;
            }

            var stereotypeX = x.Stereotype.ToEnum<Stereotypes>();
            var stereotypeY = y.Stereotype.ToEnum<Stereotypes>();

            switch (stereotypeX)
            {
                case Stereotypes.Abort:
                    return 1;

                case Stereotypes.Exit:
                    if (stereotypeY == Stereotypes.Abort)
                    {
                        return -1;
                    }
                    return 1;

                case Stereotypes.Finish:
                    if (stereotypeY == Stereotypes.Continue)
                    {
                        return 1;
                    }
                    return -1;

                case Stereotypes.Continue:
                    return -1;

                default:
                    return 0;
            }
        }
    }
}
