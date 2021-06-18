using Unity;
using System;
using CleanMachine;

namespace Sequentials.Builders
{
    public class LinkBinder : Binder
    {
        public string GuardName { get; set; }

        public Func<IUnityContainer, bool> GuardCondition { get; set; }

        public ActionNode LinkSupplier { get; set; }
    }
}
