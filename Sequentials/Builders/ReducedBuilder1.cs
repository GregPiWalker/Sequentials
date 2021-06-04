using System;
using Unity;

namespace Sequentials.Builders
{
    public class ReducedBuilder1 : InstructionBuilderBase
    {
        internal ReducedBuilder1()
        {
        }

        internal ReducedBuilder1(InstructionBuilderBase other)
        {
            TakeFrom(other);
        }

        public ReducedBuilder2 JumpIf(string jumpDestName, string ifName, Func<bool> ifCondition, params string[] reflexKeys)
        {
            AddJumpIf(jumpDestName, ifName, ifCondition, reflexKeys);
            return new ReducedBuilder2(this);
        }

        public ReducedBuilder2 Do(string actionName, Action<IUnityContainer> action)
        {
            AddDo(actionName, action);
            return new ReducedBuilder2(this);
        }

        public ReducedBuilder2 IfThen(string ifName, Func<bool> ifCondition, string thenName, Action<IUnityContainer> thenBehavior, params string[] reflexKeys)
        {
            AddIfThen(ifName, ifCondition, thenName, thenBehavior, reflexKeys);
            return new ReducedBuilder2(this);
        }

        public ReducedBuilder2 IfThenElse(string ifName, Func<bool> ifCondition, string thenName, Action<IUnityContainer> thenBehavior, string elseName, Action<IUnityContainer> elseBehavior, params string[] reflexKeys)
        {
            AddIfThenElse(ifName, ifCondition, thenName, thenBehavior, elseName, elseBehavior, reflexKeys);
            return new ReducedBuilder2(this);
        }

        public ReducedBuilder1 When(string whenName, Func<bool> whenCondition, params string[] reflexKeys)
        {
            AddWhen(whenName, whenCondition, reflexKeys);
            return this;
        }

        public ReducedBuilder1 OrWhen(string conditionName, Func<bool> condition, params string[] reflexKeys)
        {
            AddOrWhen(conditionName, condition, reflexKeys);
            return this;
        }

        public ReducedBuilder3 Finish()
        {
            AddFinish();
            return new ReducedBuilder3(this);
        }

        public ReducedBuilder3 FinishWhen(string finishName, Func<bool> finishCondition, params string[] reflexKeys)
        {
            AddFinish(finishName, finishCondition, reflexKeys);
            return new ReducedBuilder3(this);
        }
    }
}
