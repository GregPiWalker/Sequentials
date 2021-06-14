using System;
using Unity;

namespace Sequentials.Builders
{
    public class ReducedBuilder3 : InstructionBuilderBase
    {
        internal ReducedBuilder3()
        {
        }

        internal ReducedBuilder3(InstructionBuilderBase other)
        {
            TakeFrom(other);
        }

        public void ExitAnytime(string exitName, Func<IUnityContainer, bool> exitCondition, params string[] reflexKeys)
        {
            _exitBinder.ReflexKeys = reflexKeys;
            _exitBinder.GuardName = exitName;
            _exitBinder.GuardCondition = exitCondition;
        }
    }
}
