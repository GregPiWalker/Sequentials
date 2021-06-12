using CleanMachine;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Sequentials
{
    public class Link : Transition, IComparer<Link>
    {
        public Link(string context, string stereotype, ActionNode consumer, ILog logger)
            : base(context, stereotype, logger)
        {
            To = consumer;
            Editable = true;
        }

        public Link(string context, string stereotype, ILog logger)
            : base(context, stereotype, logger)
        {
            Editable = true;
        }

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

        internal void Connect(ActionNode supplier, ActionNode consumer)
        {
            From = supplier;
            To = consumer;

            From.AddTransition(this);
        }
    }
}
