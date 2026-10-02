using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using Mind8Kernel;

var tests = new (string Name, Action Run)[]
{
    ("extensionless path is resolved", TestExtensionlessPath),
    ("uppercase extension is accepted", TestUppercaseExtension),
    ("existing sample MCO follows runtime metadata gate", TestExistingSample),
    ("incompatible version is rejected", () => TestRejected((header, bigEndian) => header[3] = 0x70)),
    ("incompatible runtime is rejected", () => TestRejected((header, bigEndian) => header[20] = 11)),
    ("incompatible serial is rejected", () => TestRejected((header, bigEndian) => WriteUInt16(header, 22, 99, bigEndian))),
    ("missing MAIN is rejected", () => TestRejected((header, bigEndian) => WriteUInt16(header, 4, 0, bigEndian))),
    ("invalid LOC size is rejected", () => TestRejected((header, bigEndian) => WriteUInt32(header, 12, 6, bigEndian))),
    ("truncated payload is rejected", TestTruncatedPayload),
    ("short information table is rejected", TestShortFile),
    ("big-endian M-code and LOC values are converted", TestBigEndianConversion),
    ("truncated endian table is rejected", TestTruncatedEndianTable),
    ("32-bit M-code values are fully read", TestMcodeIntFetch),
    ("byte search handles null-terminated strings", TestByteSearch),
    ("hello output matches the C kernel baseline", TestHelloOutputMatchesCReference),
    ("stack reset resumes at the next instruction with reset stacks", TestStackResetResume),
    ("loaded MCO resumes after stack reset", TestMcoStackResetResume),
    ("finish dispatcher exits once", () => TestDispatchExit(0x0017, 0)),
    ("stack data count exits once", () => TestDispatchExit(0x0018, 0)),
    ("emergency exit preserves its exit code", () => TestDispatchExit(0x0013, 1)),
    ("process exit preserves its requested code", TestProcessExitCode),
};

int failures = 0;
foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
return failures == 0 ? 0 : 1;

static void TestExtensionlessPath()
{
    using var fixture = new McoFixture(CreateMco());
    Assert(Load(fixture.PathWithoutExtension).Loaded, "extensionless MCO was rejected");
}

static void TestUppercaseExtension()
{
    using var fixture = new McoFixture(CreateMco());
    string uppercasePath = Path.ChangeExtension(fixture.Path, ".MCO");
    Assert(Load(uppercasePath).Loaded, "uppercase .MCO extension was rejected");
}

static void TestExistingSample()
{
    string samplePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "hellolevel7.mco");
    Assert(File.Exists(samplePath), "existing sample MCO was not found from the workspace root");
    byte[] contents = File.ReadAllBytes(samplePath);
    ReadOnlySpan<byte> header = contents.AsSpan(contents.Length - 32);
    bool compatible = (header[3] >> 4) == 8
        && header[20] == 10
        && BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(22, 2)) == 42;
    Assert(Load(samplePath).Loaded == compatible, "existing sample MCO was not handled according to its runtime metadata");
}

static void TestRejected(Action<byte[], bool> mutateHeader)
{
    using var fixture = new McoFixture(CreateMco(mutateHeader: mutateHeader));
    Assert(!Load(fixture.Path).Loaded, "invalid MCO was accepted");
}

static void TestTruncatedPayload()
{
    byte[] bytes = CreateMco();
    Array.Resize(ref bytes, bytes.Length - 33);
    using var fixture = new McoFixture(bytes);
    Assert(!Load(fixture.Path).Loaded, "truncated MCO payload was accepted");
}

static void TestShortFile()
{
    using var fixture = new McoFixture(new byte[31]);
    Assert(!Load(fixture.Path).Loaded, "short MCO file was accepted");
}

static void TestBigEndianConversion()
{
    using var fixture = new McoFixture(CreateMco(bigEndian: true, endianInfo: [0x60]));
    LoadResult result = Load(fixture.Path);
    Assert(result.Loaded, "valid big-endian MCO was rejected");
    Assert(result.Mcode[0] == 0x22 && result.Mcode[1] == 0x11, "16-bit M-code value was not byte-swapped");
    Assert(result.Mcode[2] == 0x66 && result.Mcode[3] == 0x55
        && result.Mcode[4] == 0x44 && result.Mcode[5] == 0x33, "32-bit M-code value was not byte-swapped");
    Assert(BinaryPrimitives.ReadUInt32LittleEndian(result.LocTable) == 0x12345678, "LOC value was not byte-swapped");
}

static void TestTruncatedEndianTable()
{
    using var fixture = new McoFixture(CreateMco(bigEndian: true, includeEndianInfo: false));
    Assert(!Load(fixture.Path).Loaded, "missing endian information table was accepted");
}

static void TestMcodeIntFetch()
{
    var pointer = new MCodePointer();
    pointer.SetupMcodeArray([0x78, 0x56, 0x34, 0x12]);

    Assert(pointer.FetchMcodeInt() == 0x12345678, "32-bit M-code value was truncated");
    Assert(pointer.GetMcodeIndex() == 4, "32-bit M-code fetch advanced by an unexpected amount");
}

static void TestByteSearch()
{
    var pointer = new MCodePointer();
    pointer.SetupMcodeArray([0x10]);
    uint filenameIndex = pointer.AppendBytes([0x48, 0x69, 0x00]);

    Assert(pointer.SearchByteDirectForward(filenameIndex, uint.MaxValue, 0) == 3,
        "byte search did not stop at the null terminator");
}

static void TestHelloOutputMatchesCReference()
{
    string helloPath = Path.Combine(Directory.GetCurrentDirectory(), "hello.mco");
    byte[] helloMco = File.ReadAllBytes(helloPath);
    int headerOffset = helloMco.Length - 32;
    WriteUInt16(helloMco, headerOffset + 22, 42, bigEndian: false);
    using var fixture = new McoFixture(helloMco);

    TextWriter originalOutput = Console.Out;
    TextWriter originalError = Console.Error;
    using var csharpOutput = new StringWriter();
    Console.SetOut(csharpOutput);
    Console.SetError(TextWriter.Null);
    int csharpExitCode;
    try
    {
        csharpExitCode = new Dispatcher().Main(fixture.Path);
    }
    finally
    {
        Console.SetOut(originalOutput);
        Console.SetError(originalError);
    }

    string output = csharpOutput.ToString();
    string cKernelPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "mind8kernel", "obj", "kernel.exe"));
    if (!File.Exists(cKernelPath))
    {
        Assert(csharpExitCode == 0, $"C# hello exit code was {csharpExitCode}, expected 0");
        Assert(output == "Hello by mind8", $"C# hello output differs from the C reference: [{output}]");
        return;
    }

    var startInfo = new ProcessStartInfo(cKernelPath)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add(fixture.Path);
    using Process cKernel = Process.Start(startInfo) ?? throw new InvalidOperationException("C kernel failed to start");
    Task<string> cOutputTask = cKernel.StandardOutput.ReadToEndAsync();
    Task<string> cErrorTask = cKernel.StandardError.ReadToEndAsync();
    if (!cKernel.WaitForExit(10000))
    {
        cKernel.Kill(entireProcessTree: true);
        throw new InvalidOperationException("C kernel did not exit within 10 seconds");
    }

    string cOutput = cOutputTask.GetAwaiter().GetResult();
    string cError = cErrorTask.GetAwaiter().GetResult();
    Assert(cKernel.ExitCode == csharpExitCode,
        $"C/C# exit code mismatch: C={cKernel.ExitCode}, C#={csharpExitCode}; C stderr=[{cError}]");
    Assert(cOutput == output, $"C/C# stdout mismatch: C=[{cOutput}], C#=[{output}]");
}

static void TestStackResetResume()
{
    DispatchFixture fixture = CreateDispatcher(0x0016, 0x0015, 0x0018);
    fixture.DataStack.PushUl(123);
    fixture.ReturnStack.PushUl(456);

    int result = fixture.Dispatch();

    Assert(result == 0, "dispatcher did not terminate after the resumed instruction");
    Assert(fixture.McodePointer.GetMcodeIndex() == 0x76, "dispatch did not resume at the instruction after stack reset");
    Assert(fixture.DataStack.GetCurrentStackPointer() == fixture.InitialDataStackPointer - 2,
        "data stack was not reset before the stack check ran");
    Assert(fixture.DataStack.GetUl() == 0 && fixture.DataStack.GetUlOffset(1) == 1,
        "stack check did not observe the reset data stack");
    Assert(fixture.ReturnStack.GetCurrentStackPointer() == fixture.InitialReturnStackPointer,
        "return stack was not reset");
}

static void TestMcoStackResetResume()
{
    using var fixture = new McoFixture(CreateMco(executionOpcodes: [0x0016, 0x0015, 0x0018]));

    int result = new Dispatcher().Main(fixture.Path);

    Assert(result == 0, "loaded MCO did not resume and finish after stack reset");
}

static void TestDispatchExit(ushort opcode, int expectedExitCode)
{
    DispatchFixture fixture = CreateDispatcher(opcode, 0x0012);

    int result = fixture.Dispatch();

    Assert(result == expectedExitCode, "dispatch exit returned an unexpected code");
    Assert(fixture.McodePointer.GetMcodeIndex() == 0x72, "dispatch continued after its exit handler");
}

static void TestProcessExitCode()
{
    DispatchFixture fixture = CreateDispatcher(0x0014, 0x0012);
    fixture.DataStack.PushUl(37);
    fixture.DataStack.PushUl(0);

    int result = fixture.Dispatch();

    Assert(result == 37, "process exit did not preserve the requested exit code");
    Assert(fixture.McodePointer.GetMcodeIndex() == 0x72, "dispatcher resumed after process exit");
}

static DispatchFixture CreateDispatcher(params ushort[] opcodes)
{
    var dispatcher = new Dispatcher();
    Type dispatcherType = typeof(Dispatcher);
    var mcode = new byte[256];
    for (int index = 0; index < opcodes.Length; index++)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(mcode.AsSpan(0x70 + index * 2, 2), opcodes[index]);
    }

    var mcodePointer = new MCodePointer();
    mcodePointer.SetupMcodeArray(mcode);
    mcodePointer.ResetMcodeIndex(0x70);
    var dataStack = new StackPointer();
    dataStack.SetupStackArray(64);
    var returnStack = new StackPointer();
    returnStack.SetupStackArray(64);
    SetField(dispatcherType, dispatcher, "McodeBase", mcode);
    SetField(dispatcherType, dispatcher, "mp", mcodePointer);
    SetField(dispatcherType, dispatcher, "dtp", dataStack);
    SetField(dispatcherType, dispatcher, "rtp", returnStack);

    MethodInfo setupFunctions = dispatcherType.GetMethod("SetupCsFunctions", BindingFlags.Instance | BindingFlags.NonPublic)!;
    object?[] functionArguments = [Array.Empty<Action>()];
    setupFunctions.Invoke(dispatcher, functionArguments);
    Action[] functions = (Action[])functionArguments[0]!;

    return new DispatchFixture(dispatcher, mcodePointer, dataStack, returnStack, functions,
        dataStack.GetCurrentStackPointer(), returnStack.GetCurrentStackPointer());
}

static void SetField(Type declaringType, object target, string name, object value)
{
    declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}

static LoadResult Load(string filename)
{
    var dispatcher = new Dispatcher();
    Type dispatcherType = typeof(Dispatcher);
    Type infoType = dispatcherType.GetNestedType("McodeInfoStruct", BindingFlags.NonPublic)!;
    MethodInfo setup = dispatcherType.GetMethod("SetupForDispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
    object?[] arguments = [Activator.CreateInstance(infoType), filename];
    bool loaded = (bool)setup.Invoke(dispatcher, arguments)!;
    FieldInfo mcodeField = dispatcherType.GetField("McodeBase", BindingFlags.Instance | BindingFlags.NonPublic)!;
    FieldInfo locField = dispatcherType.GetField("WordOffsetTable", BindingFlags.Instance | BindingFlags.NonPublic)!;
    return new LoadResult(loaded, (byte[])mcodeField.GetValue(dispatcher)!, (byte[])locField.GetValue(dispatcher)!);
}

static byte[] CreateMco(
    bool bigEndian = false,
    byte[]? endianInfo = null,
    ushort[]? executionOpcodes = null,
    bool includeEndianInfo = true,
    Action<byte[], bool>? mutateHeader = null)
{
    const int mcodeSize = 128;
    const int locTableSize = 4;
    byte[] mcode = new byte[mcodeSize];
    byte[] locTable = new byte[locTableSize];
    if (bigEndian)
    {
        mcode[0] = 0x11;
        mcode[1] = 0x22;
        mcode[2] = 0x33;
        mcode[3] = 0x44;
        mcode[4] = 0x55;
        mcode[5] = 0x66;
        BinaryPrimitives.WriteUInt32BigEndian(locTable, 0x12345678);
    }
    else
    {
        BinaryPrimitives.WriteUInt32LittleEndian(locTable, 0x70);
    }
    if (executionOpcodes is not null)
    {
        for (int index = 0; index < executionOpcodes.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mcode.AsSpan(0x70 + index * 2, 2), executionOpcodes[index]);
        }
    }

    byte[] header = new byte[32];
    header[0] = (byte)'M';
    header[1] = (byte)'C';
    header[2] = bigEndian ? (byte)0x20 : (byte)0;
    header[3] = 0x80;
    WriteUInt16(header, 4, 1, bigEndian);
    WriteUInt32(header, 8, mcodeSize, bigEndian);
    WriteUInt32(header, 12, locTableSize, bigEndian);
    WriteUInt32(header, 16, 64, bigEndian);
    header[20] = 10;
    WriteUInt16(header, 22, 42, bigEndian);
    WriteUInt32(header, 24, 256, bigEndian);
    WriteUInt32(header, 28, 256, bigEndian);
    mutateHeader?.Invoke(header, bigEndian);

    byte[] edi = new byte[mcodeSize / 8];
    if (endianInfo is not null)
    {
        Array.Copy(endianInfo, edi, Math.Min(endianInfo.Length, edi.Length));
    }
    int ediLength = bigEndian && includeEndianInfo ? edi.Length : 0;
    byte[] file = new byte[mcode.Length + locTable.Length + ediLength + header.Length];
    int offset = 0;
    mcode.CopyTo(file, offset);
    offset += mcode.Length;
    locTable.CopyTo(file, offset);
    offset += locTable.Length;
    if (ediLength > 0)
    {
        Array.Copy(edi, 0, file, offset, ediLength);
        offset += ediLength;
    }
    header.CopyTo(file, offset);
    return file;
}

static void WriteUInt16(byte[] destination, int offset, ushort value, bool bigEndian)
{
    if (bigEndian)
    {
        BinaryPrimitives.WriteUInt16BigEndian(destination.AsSpan(offset, 2), value);
    }
    else
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination.AsSpan(offset, 2), value);
    }
}

static void WriteUInt32(byte[] destination, int offset, uint value, bool bigEndian)
{
    if (bigEndian)
    {
        BinaryPrimitives.WriteUInt32BigEndian(destination.AsSpan(offset, 4), value);
    }
    else
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, 4), value);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed record LoadResult(bool Loaded, byte[] Mcode, byte[] LocTable);

sealed record DispatchFixture(
    Dispatcher Dispatcher,
    MCodePointer McodePointer,
    StackPointer DataStack,
    StackPointer ReturnStack,
    Action[] Functions,
    uint InitialDataStackPointer,
    uint InitialReturnStackPointer)
{
    public int Dispatch()
    {
        Type dispatcherType = typeof(Dispatcher);
        Type infoType = dispatcherType.GetNestedType("McodeInfoStruct", BindingFlags.NonPublic)!;
        MethodInfo dispatch = dispatcherType.GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object?[] arguments = [Activator.CreateInstance(infoType), Functions];
        return (int)dispatch.Invoke(Dispatcher, arguments)!;
    }
}

sealed class McoFixture : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mind8-mco-{Guid.NewGuid():N}");

    public McoFixture(byte[] contents)
    {
        Directory.CreateDirectory(directory);
        Path = System.IO.Path.Combine(directory, "fixture.mco");
        File.WriteAllBytes(Path, contents);
    }

    public string Path { get; }
    public string PathWithoutExtension => System.IO.Path.Combine(directory, "fixture");

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }
}