using System;

namespace Sequentials
{
    public enum Stereotypes
    {
        Exit,
        Abort,
        Finish,
        Continue,
        NoOp,
        Decision
    }

    public enum TriggerKey
    {
        InstrumentState,
        DoorState,
        ChipPresence
    }

    public enum SequenceState
    {
        Assembling,
        Ready,
        Running,
        Paused,
        Done
    }

    public enum SequenceResult
    {
        Unknown,
        Completed,
        Exited,
        Aborted,
    }

    public enum Phase
    {
        Mutable,
        Immutable
    }
}
