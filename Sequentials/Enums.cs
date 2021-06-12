using System;

namespace Sequentials
{
    public enum Stereotypes
    {
        Exit,
        Abort,
        Finish,
        Continue
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
        Finished,
        Exited,
        Aborted,
    }

    public enum Phase
    {
        Mutable,
        Immutable
    }
}
