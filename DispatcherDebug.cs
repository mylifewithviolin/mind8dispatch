namespace Mind8Kernel;

public sealed record DispatcherSnapshot(uint Offset, uint[] DataStack, uint[] ReturnStack, byte[] Data);

public partial class Dispatcher
{
    public bool TraceEnabled { get; set; } = true;
    public Action<Dispatcher>? BeforeInstruction { get; set; }
    public uint DebugInstructionOffset => mp.GetMcodeIndex();

    public DispatcherSnapshot CaptureSnapshot() =>
        new(mp.GetMcodeIndex(), dtp.CaptureDebugValues(), rtp.CaptureDebugValues(), ap.CaptureDebugData());
}
