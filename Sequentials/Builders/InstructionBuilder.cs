using System;
using Unity;

namespace Sequentials.Builders
{
    public class InstructionBuilder : InstructionBuilderBase
    {
        public ReducedBuilder1 Start(string actionName, Action<IUnityContainer> action)
        {
            AddStart(actionName, action);
            return new ReducedBuilder1(this);
        }

        public ReducedBuilder1 StartWhen(string whenName, Func<bool> whenCondition, params string[] reflexKeys)
        {
            AddStartWhen(whenName, whenCondition, reflexKeys);
            return new ReducedBuilder1(this);
        }
    }
}
