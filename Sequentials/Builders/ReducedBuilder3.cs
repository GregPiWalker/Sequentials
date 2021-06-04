using System;

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

        public void ExitAnytime(string exitName, Func<bool> exitCondition, params string[] reflexKeys)
        {
            _finishName = exitName;
            _finishCondition = exitCondition;
            _finishReflexKeys = reflexKeys;
        }
    }
}
