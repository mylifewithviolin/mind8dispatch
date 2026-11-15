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
    ("development serial is rejected", () => TestRejected((header, bigEndian) => WriteUInt16(header, 22, 46, bigEndian))),
    ("missing MAIN is rejected", () => TestRejected((header, bigEndian) => WriteUInt16(header, 4, 0, bigEndian))),
    ("invalid LOC size is rejected", () => TestRejected((header, bigEndian) => WriteUInt32(header, 12, 6, bigEndian))),
    ("truncated payload is rejected", TestTruncatedPayload),
    ("short information table is rejected", TestShortFile),
    ("big-endian M-code and LOC values are converted", TestBigEndianConversion),
    ("truncated endian table is rejected", TestTruncatedEndianTable),
    ("32-bit M-code values are fully read", TestMcodeIntFetch),
    ("byte search handles null-terminated strings", TestByteSearch),
    ("stack offset read is safe before values are pushed", TestEmptyStackOffsetRead),
    ("local byte offsets preserve adjacent values and frame", TestLocalByteOffsets),
    ("local scalar opcodes preserve the return frame", TestLocalScalarOpcodes),
    ("local pair opcodes preserve the return frame", TestLocalPairOpcodes),
    ("empty stack pop reports underflow without changing state", TestPopUnderflow),
    ("unimplemented opcode identifies its location", TestUnsupportedOpcode),
    ("integer output preserves signed 64-bit values and LF", TestIntegerOutput),
    ("greater-than compares signed operands in order", TestGreaterThan),
    ("boolean operations preserve numeric stack layout", TestBooleanOperations),
    ("integer arithmetic updates the integer word", TestIntegerArithmetic),
    ("string variable preserves address and length", TestStringVariable),
    ("string extraction counts Shift-JIS characters in data", TestStringExtraction),
    ("string equality compares code and data regions", TestStringEquality),
    ("local string extraction updates the return frame", TestLocalStringExtraction),
    ("local access pointer survives stack movement and resets", TestLocalAccessPointer),
    ("last character handles empty and Shift-JIS strings", TestLastCharacter),
    ("swap preserves both words of each value", TestSwap),
    ("memory clear addresses the data region", TestClearDataMemory),
    ("byte searches address data and count from the proper end", TestDataByteSearch),
    ("hello fixture matches reference bytes", () => TestFixtureOutput("hello")),
    ("variables fixture matches reference bytes", () => TestFixtureOutput("variables")),
    ("binarysearch fixture matches reference bytes", () => TestFixtureOutput("binarysearch")),
    ("bubblesort fixture matches reference bytes", () => TestFixtureOutput("bubblesort")),
    ("insertsort fixture matches reference bytes", () => TestFixtureOutput("insertsort")),
    ("quad array access honors element bounds and both words", TestQuadArrayAccess),
    ("hello output matches the C kernel baseline", TestHelloOutputMatchesCReference),
    ("legacy serial-46 samples are rejected", TestAllSampleOutputs),
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

static void TestEmptyStackOffsetRead()
{
    var stack = new StackPointer();
    stack.SetupStackArray(64);

    Assert(stack.GetUlOffset(1) == 0, "empty stack offset read did not return the empty stack value");

    stack.PushUl(123);
    stack.PushUl(456);
    Assert(stack.GetUlOffset(1) == 123, "offset read did not resolve to the previous stack value");
}

static void TestLocalByteOffsets()
{
    var stack = new StackPointer();
    stack.SetupStackArray(64);
    stack.PushUl(0x12345678);
    stack.MoveStackPointer(-3);
    uint pointer = stack.GetCurrentStackPointer();
    stack.WriteLocal(0, 0x11223344, 4);
    stack.WriteLocal(2, 0xABCD, 2);
    stack.WriteLocal(4, 0x55667788, 4);
    stack.WriteLocal(5, 0xEF, 1);
    stack.WriteLocal(9, 0xCAFE, 2);
    Assert(stack.ReadLocal(0, 4) == 0xABCD3344, "16-bit write damaged adjacent bytes");
    Assert(stack.ReadLocal(4, 4) == 0x5566EF88, "8-bit write damaged adjacent bytes");
    Assert(stack.ReadLocal(9, 2) == 0xCAFE, "unaligned local access failed");
    Assert(stack.ReadLocal(12, 4) == 0x12345678, "local write damaged the return address");
    Assert(stack.GetCurrentStackPointer() == pointer, "local access moved the stack pointer");
}

static void TestLocalScalarOpcodes()
{
    foreach ((ushort write, ushort read, uint expected) in new (ushort, ushort, uint)[]
        { (0x0069, 0x0053, 0x78), (0x006A, 0x0054, 0x5678), (0x006B, 0x0055, 0x12345678) })
    {
        DispatchFixture fixture = CreateDispatcher(write, 2, read, 2, 0x0017);
        fixture.ReturnStack.PushUl(0x76543210);
        fixture.ReturnStack.MoveStackPointer(-2);
        uint pointer = fixture.ReturnStack.GetCurrentStackPointer();
        fixture.DataStack.PushUl(0x12345678);
        fixture.DataStack.PushUl(0);
        Assert(fixture.Dispatch() == 0, "local scalar dispatch failed");
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"local scalar opcode 0x{write:X4} returned an incorrect value");
        Assert(fixture.ReturnStack.GetCurrentStackPointer() == pointer, "scalar access moved the frame");
        Assert(fixture.ReturnStack.ReadLocal(8, 4) == 0x76543210, "scalar access overwrote the return address");
    }
}

static void TestLocalPairOpcodes()
{
    foreach ((ushort write, ushort read) in new (ushort, ushort)[]
        { (0x006C, 0x0056), (0x006D, 0x0057), (0x006E, 0x0058) })
    {
        DispatchFixture fixture = CreateDispatcher(write, 0, read, 0, 0x0017);
        fixture.ReturnStack.PushUl(0x76543210);
        fixture.ReturnStack.MoveStackPointer(-2);
        uint pointer = fixture.ReturnStack.GetCurrentStackPointer();
        fixture.DataStack.PushUl(0x12345678);
        fixture.DataStack.PushUl(0xABCDEF01);
        Assert(fixture.Dispatch() == 0, "local pair dispatch failed");
        uint expectedFirst = write == 0x006C ? 0x12345678 : 0xABCDEF01;
        uint expectedSecond = write == 0x006C ? 0xABCDEF01 : 0x12345678;
        Assert(fixture.ReturnStack.ReadLocal(0, 4) == expectedFirst
            && fixture.ReturnStack.ReadLocal(4, 4) == expectedSecond, "local pair memory layout differs from the C kernel");
        Assert(fixture.DataStack.PopUl() == 0xABCDEF01 && fixture.DataStack.PopUl() == 0x12345678,
            $"local pair opcode 0x{write:X4} changed the pair order");
        Assert(fixture.ReturnStack.GetCurrentStackPointer() == pointer, "pair access moved the frame");
        Assert(fixture.ReturnStack.ReadLocal(8, 4) == 0x76543210, "pair access overwrote the return address");
    }
}

static void TestPopUnderflow()
{
    var stack = new StackPointer();
    stack.SetupStackArray(64);
    uint pointer = stack.GetCurrentStackPointer();
    foreach (Action pop in new Action[] { () => stack.PopUb(), () => stack.PopUs(), () => stack.PopUl() })
    {
        bool failed = false;
        try { pop(); }
        catch (InvalidOperationException) { failed = true; }
        Assert(failed, "empty pop did not report stack underflow");
        Assert(stack.GetCurrentStackPointer() == pointer, "empty pop changed the stack pointer");
    }
}

static void TestUnsupportedOpcode()
{
    DispatchFixture fixture = CreateDispatcher(0x7FFF);
    try { fixture.Dispatch(); }
    catch (TargetInvocationException exception) when (exception.InnerException is NotSupportedException unsupported)
    {
        Assert(unsupported.Message.Contains("0x7FFF") && unsupported.Message.Contains("0x70"),
            "unsupported opcode diagnostic omitted the opcode or byte location");
        return;
    }
    throw new InvalidOperationException("unsupported opcode was accepted");
}

static void TestIntegerOutput()
{
    DispatchFixture fixture = CreateDispatcher(0x00C9, 0x00C4, 0x0017);
    fixture.DataStack.PushUl(0xFFFFFFFF);
    fixture.DataStack.PushUl(0xFFFFFFFF);
    TextWriter original = Console.Out;
    using var output = new StringWriter();
    try
    {
        Console.SetOut(output);
        Assert(fixture.Dispatch() == 0, "integer output failed");
    }
    finally
    {
        Console.SetOut(original);
    }
    Assert(output.ToString() == "-1\n", "integer output or newline differs");
}

static void TestGreaterThan()
{
    foreach ((int left, int right, uint expected) in new (int, int, uint)[] { (-1, 0, 0), (0, -1, 1), (2, 2, 0) })
    {
        DispatchFixture fixture = CreateDispatcher(0x0112, 0x0017);
        fixture.DataStack.PushUl(unchecked((uint)left));
        fixture.DataStack.PushUl(0);
        fixture.DataStack.PushUl(unchecked((uint)right));
        fixture.DataStack.PushUl(0);
        Assert(fixture.Dispatch() == 0, "comparison dispatch failed");
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            "signed comparison produced an incorrect result");
    }
}

static void TestQuadArrayAccess()
{
    DispatchFixture fixture = CreateDispatcher(0x00D6, 8, 0, 3, 0, 0x0048, 0x0017);
    var data = new AccessPointer();
    data.SetupDataArray(32);
    data.ResetDataIndex(16);
    data.SetUll(0x12345678ABCDEF01);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
    fixture.DataStack.PushUl(2);
    fixture.DataStack.PushUl(0);
    Assert(fixture.Dispatch() == 0, "quad array access failed");
    Assert(fixture.DataStack.PopUl() == 0x12345678 && fixture.DataStack.PopUl() == 0xABCDEF01,
        "quad array access did not preserve both words");

    foreach (uint index in new uint[] { 0, 4 })
    {
        fixture = CreateDispatcher(0x00D6, 8, 0, 3, 0, 0x0017);
        fixture.DataStack.PushUl(index);
        fixture.DataStack.PushUl(0);
        try { fixture.Dispatch(); }
        catch (TargetInvocationException exception) when (exception.InnerException is IndexOutOfRangeException) { continue; }
        throw new InvalidOperationException($"invalid array index {index} was accepted");
    }
}

static void TestBooleanOperations()
{
    foreach ((uint left, uint right, uint expected) in new (uint, uint, uint)[] { (0, 3, 0), (2, 3, 1) })
    {
        DispatchFixture fixture = CreateDispatcher(0x00FD, 0x0100, 0x0017);
        fixture.DataStack.PushUl(left);
        fixture.DataStack.PushUl(0);
        fixture.DataStack.PushUl(right);
        fixture.DataStack.PushUl(0);
        Assert(fixture.Dispatch() == 0, "boolean dispatch failed");
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 1 - expected,
            "boolean operation produced an incorrect result");
    }
}

static void TestIntegerArithmetic()
{
    DispatchFixture fixture = CreateDispatcher(0x010A, 0x0105, 5, 0x0017);
    fixture.DataStack.PushUl(15);
    fixture.DataStack.PushUl(0);
    fixture.DataStack.PushUl(1);
    fixture.DataStack.PushUl(0);
    Assert(fixture.Dispatch() == 0, "integer arithmetic dispatch failed");
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 19,
        "integer arithmetic changed the wrong word");

    fixture = CreateDispatcher(0x010C, 0x0017);
    fixture.DataStack.PushUl(15);
    fixture.DataStack.PushUl(0);
    fixture.DataStack.PushUl(2);
    fixture.DataStack.PushUl(0);
    Assert(fixture.Dispatch() == 0, "integer division dispatch failed");
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 7,
        "integer division produced an incorrect result");
}

static void TestStringVariable()
{
    DispatchFixture fixture = CreateDispatcher(0x0068, 0, 0, 0x0051, 0, 0, 0x0017);
    var data = new AccessPointer();
    data.SetupDataArray(16);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
    fixture.DataStack.PushUl(123);
    fixture.DataStack.PushUl(6);
    Assert(fixture.Dispatch() == 0, "string variable dispatch failed");
    Assert(fixture.DataStack.PopUl() == 6 && fixture.DataStack.PopUl() == 123,
        "string address and length were not preserved");
}

static void TestStringExtraction()
{
    DispatchFixture fixture = CreateDispatcher(0x0052, 0, 0, 0x0096, 0x01D5, 0x0017);
    var data = new AccessPointer();
    data.SetupDataArray(16);
    data.SetUl4Ms(3, 0);
    data.ResetDataIndex(8);
    data.SetUb(0x82);
    data.ResetDataIndex(9);
    data.SetUb(0xA0);
    data.ResetDataIndex(10);
    data.SetUb((byte)'X');
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
    Assert(fixture.Dispatch() == 0, "string extraction dispatch failed");
    Assert(fixture.DataStack.PopUl() == 2 && fixture.DataStack.PopUl() == 0x80000008,
        "string extraction did not retain the data address and complete character");
}

static void TestStringEquality()
{
    DispatchFixture fixture = CreateDispatcher(0x01DD, 0x0017);
    uint address = fixture.McodePointer.AppendBytes(new byte[] { 0x82, 0xA0 });
    var data = new AccessPointer();
    data.SetupDataArray(2);
    data.SetUb(0x82);
    data.ResetDataIndex(1);
    data.SetUb(0xA0);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
    fixture.DataStack.PushUl(address);
    fixture.DataStack.PushUl(2);
    fixture.DataStack.PushUl(0x80000000);
    fixture.DataStack.PushUl(2);
    Assert(fixture.Dispatch() == 0, "string equality dispatch failed");
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 1,
        "equal strings in different memory regions were not equal");
}

static void TestLocalStringExtraction()
{
    DispatchFixture fixture = CreateDispatcher(0x003A, 0x0096, 0x01B7, 0x0017);
    uint address = fixture.McodePointer.AppendBytes(new byte[] { (byte)'A', 0x82, 0xA0 });
    fixture.ReturnStack.MoveStackPointer(-2);
    fixture.ReturnStack.WriteLocal(0, 3, 4);
    fixture.ReturnStack.WriteLocal(4, address, 4);
    Assert(fixture.Dispatch() == 0, "local string extraction dispatch failed");
    Assert(fixture.ReturnStack.ReadLocal(0, 4) == 1 && fixture.ReturnStack.ReadLocal(4, 4) == address,
        "local string variable was not shortened");
    Assert(fixture.DataStack.PopUl() == 2 && fixture.DataStack.PopUl() == address + 1,
        "right extraction returned the wrong string");
}

static void TestLocalAccessPointer()
{
    var stack = new StackPointer();
    stack.SetupStackArray(64);
    stack.MoveStackPointer(-3);
    var pointer = new AccessPointer();
    pointer.SetupDataArray(16);
    pointer.PointStack(stack, 2);
    pointer.SetUl(0x12345678);
    stack.MoveStackPointer(-1);
    Assert(pointer.GetUl() == 0x12345678 && stack.ReadLocal(6, 4) == 0x12345678,
        "local access pointer did not retain its absolute location");
    pointer.SetupDataArray(16);
    pointer.SetUl(0xABCDEF01);
    Assert(pointer.GetUl() == 0xABCDEF01 && stack.ReadLocal(6, 4) == 0x12345678,
        "initializing the data area did not reset the access pointer");
}

static void TestLastCharacter()
{
    foreach ((byte[] bytes, uint expected) in new (byte[], uint)[]
        { (Array.Empty<byte>(), 0), (new byte[] { (byte)'A' }, 65), (new byte[] { 0x82, 0xA0 }, 0x82A0) })
    {
        DispatchFixture fixture = CreateDispatcher(0x01D2, 0x0017);
        uint address = fixture.McodePointer.AppendBytes(bytes);
        fixture.DataStack.PushUl(address);
        fixture.DataStack.PushUl((uint)bytes.Length);
        Assert(fixture.Dispatch() == 0, "last character dispatch failed");
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            "last character differs");
    }
}

static void TestSwap()
{
    DispatchFixture fixture = CreateDispatcher(0x011E, 0x0017);
    foreach (uint value in new uint[] { 1, 2, 3, 4 }) fixture.DataStack.PushUl(value);
    Assert(fixture.Dispatch() == 0, "swap dispatch failed");
    foreach (uint value in new uint[] { 2, 1, 4, 3 })
        Assert(fixture.DataStack.PopUl() == value, "swap changed value ordering");
}

static void TestClearDataMemory()
{
    DispatchFixture fixture = CreateDispatcher(0x0038, 2, 0, 3, 0, 0x0075, 0x0017);
    var data = new AccessPointer();
    data.SetupDataArray(8);
    data.SetUb4Msi(0xFF, 8);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
    Assert(fixture.Dispatch() == 0, "data memory clear dispatch failed");
    Assert(data.GetBytes(0, 8).SequenceEqual(new byte[] { 255, 255, 0, 0, 0, 255, 255, 255 }),
        "data memory clear affected the wrong range");
}

static void TestDataByteSearch()
{
    foreach ((ushort opcode, uint expected) in new (ushort, uint)[] { (0x01D7, 2), (0x01D8, 3) })
    {
        DispatchFixture fixture = CreateDispatcher(opcode, 0x0017);
        var data = new AccessPointer();
        data.SetupDataArray(4);
        data.ResetDataIndex(1);
        data.SetUb((byte)'A');
        SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
        fixture.DataStack.PushUl(0x80000000);
        fixture.DataStack.PushUl(4);
        fixture.DataStack.PushUl((byte)'A');
        fixture.DataStack.PushUl(0);
        Assert(fixture.Dispatch() == 0, "data byte search dispatch failed");
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            "data byte search returned an incorrect position");
    }
}

static void TestFixtureOutput(string name)
{
    string root = Directory.GetCurrentDirectory();
    string fixturePath = Path.Combine(root, "tests", "fixtures", name + ".mco");
    string referencePath = Path.Combine(root, "tests", "fixtures", "reference", name + ".stdout.txt");
    var startInfo = new ProcessStartInfo("dotnet")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add(typeof(Dispatcher).Assembly.Location);
    startInfo.ArgumentList.Add(fixturePath);
    using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{name} failed to start");
    using var output = new MemoryStream();
    Task outputTask = process.StandardOutput.BaseStream.CopyToAsync(output);
    Task<string> errorTask = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(30000))
    {
        process.Kill(entireProcessTree: true);
        process.WaitForExit();
        throw new InvalidOperationException($"{name} exceeded 30 seconds");
    }
    outputTask.GetAwaiter().GetResult();
    string error = errorTask.GetAwaiter().GetResult();
    Assert(process.ExitCode == 0, $"{name} exit={process.ExitCode}: {error}");
    Assert(error.Split('\n', StringSplitOptions.RemoveEmptyEntries).All(line =>
        System.Text.RegularExpressions.Regex.IsMatch(line, @"^\s*\d+\s+[0-9A-F]{4}\s")),
        $"{name} produced unexpected stderr: {error}");
    byte[] expected = File.ReadAllBytes(referencePath);
    byte[] actual = output.ToArray();
    Assert(actual.SequenceEqual(expected),
        $"{name} stdout differs from reference (actual {actual.Length} bytes, expected {expected.Length} bytes)");
}

static void TestAllSampleOutputs()
{
    string[] paths = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.mco");
    Assert(paths.Length == 10, $"expected 10 repository samples, found {paths.Length}");
    foreach (string path in paths)
    {
        Assert(!Load(path).Loaded, $"{Path.GetFileName(path)} unexpectedly passed the serial gate");
    }
}

static void TestHelloOutputMatchesCReference()
{
    string helloPath = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "hello.mco");

    TextWriter originalOutput = Console.Out;
    TextWriter originalError = Console.Error;
    using var csharpOutput = new StringWriter();
    Console.SetOut(csharpOutput);
    Console.SetError(TextWriter.Null);
    int csharpExitCode;
    try
    {
        csharpExitCode = new Dispatcher().Main(helloPath);
    }
    finally
    {
        Console.SetOut(originalOutput);
        Console.SetError(originalError);
    }

    string output = csharpOutput.ToString();
    Assert(csharpExitCode == 0, $"C# hello exit code was {csharpExitCode}, expected 0");
    Assert(output == "Hello by mind8", $"C# hello output differs from the expected output: [{output}]");
    string cKernelPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "mind8kernel", "obj", "kernel.exe"));
    if (!File.Exists(cKernelPath))
    {
        return;
    }

    var startInfo = new ProcessStartInfo(cKernelPath)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add(helloPath);
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