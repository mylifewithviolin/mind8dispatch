using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
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
    ("native stack margin preserves memory and rejects reads beyond its boundary", TestNativeStackMargin),
    ("generic exit preserves 32-bit return addresses and releases its frame", TestGenericExit),
    ("runtime arguments preserve argc and null-terminated Shift-JIS values", TestRuntimeArguments),
    ("CLI instruction tracing requires -D and excludes it from program arguments", TestCliTraceOption),
    ("output device distinguishes handles and preserves CRLF", TestOutputHandles),
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
    ("character strings retain stack references and byte order", TestCharacterStackStrings),
    ("local string addresses survive return stack movement", TestLocalStringAddresses),
    ("string storage handles all addressing forms and character limits", TestStringStorage),
    ("default string storage and headers preserve existing values", TestDefaultStringStorage),
    ("counted loops preserve counts and return frames", TestCountedLoops),
    ("double array reads and writes honor element bounds", TestDoubleArrayAccess),
    ("console integer conversions preserve signed values and stack order", TestConsoleIntegerConversions),
    ("string margins and character extraction preserve Shift-JIS boundaries", TestStringMargins),
    ("string append respects remaining capacity and overlap", TestStringAppend),
    ("string trimming, Xvar storage, and clear opcodes match C behavior", TestStringBoundaryAndXvarOpcodes),
    ("string margin opcode variants match direct and Jinfo contracts", TestStringMarginOpcodeVariants),
    ("string alignment and metadata setters match direct and Jinfo contracts", TestStringAlignmentAndMetadataOpcodes),
    ("uppercase conversion preserves ASCII and Shift-JIS character boundaries", TestUppercaseStringOpcodes),
    ("left and right byte or character deletion match Jvar, Svar, and Jinfo layouts", TestStringDeleteOpcodes),
    ("left and right byte or character extraction match Jvar, Svar, and Jinfo layouts", TestStringCutOpcodes),
    ("byte-range extraction and deletion clamp requests across Jvar, Svar, and Jinfo", TestStringByteRangeOpcodes),
    ("character-count extraction preserves Shift-JIS boundaries across Jvar, Svar, and Jinfo", TestStringCharacterRangeOpcodes),
    ("character and byte split positions return prefixes and remove the delimiter", TestStringSplitOpcodes),
    ("left and right character-range deletion preserves Shift-JIS boundaries", TestStringCharacterRangeDeleteOpcodes),
    ("left and right whitespace trimming matches ASCII and Shift-JIS C rules", TestStringWhitespaceOpcodes),
    ("word extraction skips leading whitespace and consumes one word", TestStringWordExtractionOpcodes),
    ("character counting and selection honor Shift-JIS boundaries", TestStringCharacterSelectionOpcodes),
    ("character and byte searches and string comparisons match C rules", TestStringSearchAndComparisonOpcodes),
    ("character classification, width, and whitespace match C rules", TestCharacterClassificationOpcodes),
    ("string byte count aliases and size reduction preserve stack layout", TestStringSizeOpcodes),
    ("single-character conversions preserve code and status stack layouts", TestSingleCharacterConversionOpcodes),
    ("Svar empty checks and byte or character deletion match direct string layouts", TestSvarStateAndDeletionOpcodes),
    ("Svar first and last byte or character extraction preserve string-info layout", TestSvarExtractionOpcodes),
    ("Svar byte-range extraction and deletion clamp requests and preserve stack layout", TestSvarByteRangeOpcodes),
    ("last character handles empty and Shift-JIS strings", TestLastCharacter),
    ("swap preserves both words of each value", TestSwap),
    ("memory clear addresses the data region", TestClearDataMemory),
    ("byte searches address data and count from the proper end", TestDataByteSearch),
    ("string reference opcodes address data and local stack storage", TestStringReferenceStubs),
    ("memory copy opcodes handle overlap, truncation, and remaining strings", TestMemoryCopyOpcodes),
    ("indirect byte word and double memory access uses tagged addresses", TestIndirectMemoryAccess),
    ("floating variable and test stack opcodes preserve 64-bit values", TestFloatingVariableOpcodes),
    ("floating constants, stack addresses, and integer fraction parts match their layouts", TestFloatingConstantsAndStackInfo),
    ("floating arithmetic, comparisons, and math functions match C opcode behavior", TestFloatingMathOpcodes),
    ("integer arithmetic, bit operations, packing, and conversions match C opcode behavior", TestIntegerUtilityOpcodes),
    ("console numeric display and wordStore11 match C formatting", TestConsoleNumericDisplay),
    ("floating parsing and integer fraction packing match stack and pointer layouts", TestAtofAndFractionPacking),
    ("memory dump and return-stack monitor output match C formats", TestConsoleMemoryDisplays),
    ("numeric-to-string conversions preserve signed values and C formatting", TestNumericStringConversions),
    ("data-stack dump prints pairs and resets the stack", TestDataStackDump),
    ("return-count and case opcodes preserve stack values and branch targets", TestReturnCountAndCaseOpcodes),
    ("byte and word array addressing uses one-based indexes and element widths", TestByteWordArrayAccess),
    ("extended arrays and structure records address the proper memory regions", TestExtendedArrayAndStructureAccess),
    ("hello fixture matches reference bytes", () => TestFixtureOutput("hello")),
    ("variables fixture matches reference bytes", () => TestFixtureOutput("variables")),
    ("binarysearch fixture matches native CRLF reference bytes", () => TestFixtureOutput("binarysearch", nativeNewlines: true)),
    ("bubblesort fixture matches native CRLF reference bytes", () => TestFixtureOutput("bubblesort", nativeNewlines: true)),
    ("insertsort fixture matches native CRLF reference bytes", () => TestFixtureOutput("insertsort", nativeNewlines: true)),
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

static void TestNativeStackMargin()
{
    var stack = new StackPointer();
    stack.SetupStackArray(64, 32);
    uint initial = stack.GetCurrentStackPointer();
    Assert(stack.PopUl() == 0 && stack.GetCurrentStackPointer() == initial + 1,
        "native empty pop did not advance into the allocated margin");
    stack.PushUl(0x12345678);
    Assert(stack.IsCurrentStackPointerInit() && stack.ReadAddress(initial * 4, 4) == 0x12345678,
        "push into the margin lost its value or its absolute address");
    Assert(stack.PopUl() == 0x12345678, "native pop synthesized zero instead of reading margin memory");
    for (int i = 1; i < 8; i++) stack.PopUl();
    uint pointer = stack.GetCurrentStackPointer();
    bool failed = false;
    try { stack.PopUl(); }
    catch (InvalidOperationException) { failed = true; }
    Assert(failed && stack.GetCurrentStackPointer() == pointer, "pop escaped the bounded margin");
    stack.ResetStackPointer();
    Assert(stack.IsCurrentStackPointerInit() && stack.GetCurrentStackPointer() == initial,
        "reset did not restore the native empty stack");
}

static void TestGenericExit()
{
    DispatchFixture fixture = CreateDispatcher(3);
    fixture.ReturnStack.PushUl(0x12345678);
    fixture.ReturnStack.MoveStackPointer(-2);
    fixture.Functions[0x0027]();
    Assert(fixture.McodePointer.GetMcodeIndex() == 0x12345678, "generic EXIT truncated its return address");
    Assert(fixture.ReturnStack.GetCurrentStackPointer() == fixture.InitialReturnStackPointer,
        "generic EXIT did not release its full frame");
}

static void TestRuntimeArguments()
{
    using var file = new McoFixture(CreateMco(executionOpcodes: [0x0264, 0x0017]));
    var dispatcher = new Dispatcher { TraceEnabled = false };
    Assert(dispatcher.Main(file.Path, "\u3042 \u3044", "second") == 0, "argument fixture failed");
    Type type = typeof(Dispatcher);
    var stack = (StackPointer)type.GetField("dtp", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dispatcher)!;
    var code = (MCodePointer)type.GetField("mp", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dispatcher)!;
    Assert(stack.PopUl() == 0 && stack.PopUl() == 4, "argc omitted launcher, MCO, or program arguments");
    DispatchFixture functions = CreateDispatcher();
    SetField(type, functions.Dispatcher, "argumentAddresses",
        type.GetField("argumentAddresses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dispatcher)!);
    foreach ((uint index, string expected) in new (uint, string)[] { (1, file.Path), (2, "\u3042 \u3044"), (3, "second") })
    {
        functions.DataStack.PushUl(index);
        functions.DataStack.PushUl(0);
        functions.Functions[0x0263]();
        Assert(functions.DataStack.PopUl() == 0, "argument address is not a numeric value");
        uint address = functions.DataStack.PopUl();
        uint count = code.SearchByteDirectForward(address, uint.MaxValue, 0);
        Assert(System.Text.Encoding.GetEncoding("shift_jis").GetString(code.GetUb(address, count - 1)) == expected,
            "argument bytes, spaces, or null terminator were not preserved");
    }
    functions.DataStack.PushUl(4);
    functions.DataStack.PushUl(0);
    bool failed = false;
    try { functions.Functions[0x0263](); }
    catch (ArgumentOutOfRangeException) { failed = true; }
    Assert(failed, "out-of-range argument access was accepted");

    using var cliFile = new McoFixture(CreateMco(executionOpcodes: [0x0264, 0x0014]));
    var startInfo = new ProcessStartInfo("dotnet")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add(typeof(Dispatcher).Assembly.Location);
    startInfo.ArgumentList.Add(cliFile.Path);
    startInfo.ArgumentList.Add("first argument");
    startInfo.ArgumentList.Add("\u3042");
    using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("argument CLI failed to start");
    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
    Task<string> errorTask = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(30000))
    {
        process.Kill(entireProcessTree: true);
        process.WaitForExit();
        throw new InvalidOperationException("argument CLI exceeded 30 seconds");
    }
    Assert(process.ExitCode == 4 && outputTask.GetAwaiter().GetResult() == "",
        $"CLI did not forward program arguments: {errorTask.GetAwaiter().GetResult()}");
    errorTask.GetAwaiter().GetResult();
}

static void TestCliTraceOption()
{
    using var fixture = new McoFixture(CreateMco(executionOpcodes: [0x0264, 0x0014]));
    foreach (string placement in new[] { "none", "before", "after" })
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(typeof(Dispatcher).Assembly.Location);
        if (placement == "before") startInfo.ArgumentList.Add("-D");
        startInfo.ArgumentList.Add(fixture.Path);
        if (placement == "after") startInfo.ArgumentList.Add("-D");
        startInfo.ArgumentList.Add("first argument");
        startInfo.ArgumentList.Add("\u3042");
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("trace CLI failed to start");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new InvalidOperationException("trace CLI exceeded 30 seconds");
        }
        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        Assert(process.ExitCode == 4, $"{placement}: -D changed argc or argument forwarding: {error}");
        Assert(output == "", $"{placement}: trace leaked into stdout: {output}");
        if (placement == "none")
        {
            Assert(error == "", $"CLI traced instructions without -D: {error}");
        }
        else
        {
            string[] lines = error.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert(lines.Length == 2
                && System.Text.RegularExpressions.Regex.IsMatch(lines[0],
                    @"^\s*1\s+0264\s+612\s+114(?:\s+\d+){6}\s*$")
                && System.Text.RegularExpressions.Regex.IsMatch(lines[1],
                    @"^\s*2\s+0014\s+20\s+116\s+\d+\s+0\s+4(?:\s+\d+){3}\s*$"),
                $"{placement}: instruction trace omitted opcode, offset, or stack values: {error}");
        }
    }
}

static void TestOutputHandles()
{
    DispatchFixture fixture = CreateDispatcher();
    uint address = fixture.McodePointer.AppendBytes([(byte)'A', 13, 10]);
    TextWriter originalOut = Console.Out;
    TextWriter originalError = Console.Error;
    using var output = new StringWriter();
    using var error = new StringWriter();
    try
    {
        Console.SetOut(output);
        Console.SetError(error);
        foreach (uint handle in new uint[] { 1, 2, 9 })
        {
            fixture.DataStack.PushUl(address);
            fixture.DataStack.PushUl(3);
            fixture.DataStack.PushUl(handle);
            fixture.DataStack.PushUl(0);
            if (handle != 9) fixture.Functions[0x026D]();
            else
            {
                bool failed = false;
                try { fixture.Functions[0x026D](); }
                catch (NotSupportedException) { failed = true; }
                Assert(failed, "unsupported output handle was silently accepted");
            }
        }
        Assert(output.ToString() == "A\r\n" && error.ToString() == "A\r\n",
            "output handles or native newline bytes changed");
    }
    finally
    {
        Console.SetOut(originalOut);
        Console.SetError(originalError);
    }
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

    fixture = CreateDispatcher(0x00D7, 8, 0, 3, 0, 0x0048, 0x0017);
    data = new AccessPointer();
    data.SetupDataArray(32);
    data.ResetDataIndex(16);
    data.SetUll(0x12345678ABCDEF01);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
    fixture.DataStack.PushUl(2);
    fixture.DataStack.PushUl(0);
    Assert(fixture.Dispatch() == 0, "quad array alias access failed");
    Assert(fixture.DataStack.PopUl() == 0x12345678 && fixture.DataStack.PopUl() == 0xABCDEF01,
        "quad array alias did not preserve both words");

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

static void TestByteWordArrayAccess()
{
    foreach ((ushort opcode, uint width, uint baseOffset, uint expected) in new (ushort, uint, uint, uint)[]
        { (0x00D3, 1, 4, 0xAB), (0x00D4, 2, 8, 0xCDEF) })
    {
        DispatchFixture fixture = CreateDispatcher(opcode, (ushort)baseOffset, 0, 3, 0, 0x0048, 0x0017);
        var data = new AccessPointer();
        data.SetupDataArray(32);
        data.ResetDataIndex(baseOffset + width);
        if (width == 1) data.SetUb((byte)expected);
        else data.SetUs((ushort)expected);
        SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);

        fixture.DataStack.PushUl(2);
        fixture.DataStack.PushUl(0);
        Assert(fixture.Dispatch() == 0, $"opcode 0x{opcode:X4} dispatch failed");
        uint actual = width == 1 ? data.GetUb() : data.GetUs();
        Assert(actual == expected, $"opcode 0x{opcode:X4} used an incorrect array element offset");
    }

    foreach (ushort opcode in new ushort[] { 0x00D3, 0x00D4 })
    {
        DispatchFixture fixture = CreateDispatcher(opcode, 4, 0, 3, 0, 0x0048, 0x0017);
        fixture.DataStack.PushUl(0);
        fixture.DataStack.PushUl(0);
        try { fixture.Dispatch(); }
        catch (TargetInvocationException exception) when (exception.InnerException is IndexOutOfRangeException) { continue; }
        throw new InvalidOperationException($"opcode 0x{opcode:X4} accepted a zero array index");
    }
}

static void TestExtendedArrayAndStructureAccess()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 64);
    WriteMcodeUInts(fixture, 0x70, 8, 3, 3);
    data.ResetDataIndex(14);
    data.SetUb(0xA5);
    PushIntegerQuad(fixture.DataStack, 3);
    fixture.Functions[0x00D8]();
    Assert(data.GetUb() == 0xA5, "variable-size array used an incorrect element stride");

    foreach ((ushort opcode, ushort width, ushort size) in new (ushort, ushort, ushort)[]
        { (0x00D9, 1, 1), (0x00DA, 2, 2), (0x00DB, 4, 4), (0x00DC, 8, 8), (0x00DD, 8, 8), (0x00DE, 3, 3) })
    {
        fixture = CreateDispatcher();
        uint offset = 4;
        if (opcode == 0x00DE) WriteMcodeUShorts(fixture, 0x70, (ushort)offset, 3, size);
        else WriteMcodeUShorts(fixture, 0x70, (ushort)offset, 3);
        for (int i = 0; i < 4; i++) fixture.ReturnStack.PushUl(0);
        uint target = fixture.ReturnStack.GetCurrentStackPointer() * 4 + offset + width;
        fixture.ReturnStack.WriteAddress(target, 0x302010, size);
        PushIntegerQuad(fixture.DataStack, 2);
        fixture.Functions[opcode]();
        AccessPointer pointer = GetAccessPointer(fixture);
        Assert(pointer.GetUb() == 0x10, $"local array opcode 0x{opcode:X4} used the wrong element width or stack region");
    }

    foreach ((ushort opcode, uint width, ulong value) in new (ushort, uint, ulong)[]
        { (0x00DF, 1, 0xA5), (0x00E0, 2, 0xBEEF), (0x00E1, 4, 0x12345678),
          (0x00E2, 8, 0x12345678ABCDEF01) })
    {
        fixture = CreateDispatcher();
        byte[] table = CreateNumericArrayTable(width, value);
        uint tableAddress = fixture.McodePointer.AppendBytes(table);
        WriteMcodeUInts(fixture, 0x70, tableAddress);
        AttachDataPointer(fixture, 16);
        PushIntegerQuad(fixture.DataStack, 2);
        fixture.Functions[opcode]();
        AccessPointer pointer = GetAccessPointer(fixture);
        ulong actual = width switch
        {
            1 => pointer.GetUb(),
            2 => pointer.GetUs(),
            4 => pointer.GetUl(),
            _ => pointer.GetUll(),
        };
        Assert(actual == value, $"constant array opcode 0x{opcode:X4} selected the wrong element");
    }

    fixture = CreateDispatcher();
    byte[] stringInfo = EncodeUInt32s(2, 0).Concat(new byte[] { (byte)'B', (byte)'C', 0 }).ToArray();
    uint stringAddress = fixture.McodePointer.AppendBytes(stringInfo);
    byte[] stringTable = new byte[14];
    BinaryPrimitives.WriteUInt16LittleEndian(stringTable, 3);
    BinaryPrimitives.WriteUInt32LittleEndian(stringTable.AsSpan(2), stringAddress);
    BinaryPrimitives.WriteUInt32LittleEndian(stringTable.AsSpan(6), stringAddress);
    BinaryPrimitives.WriteUInt32LittleEndian(stringTable.AsSpan(10), stringAddress);
    uint constantStringTable = fixture.McodePointer.AppendBytes(stringTable);
    WriteMcodeUInts(fixture, 0x70, constantStringTable);
    AttachDataPointer(fixture, 16);
    PushIntegerQuad(fixture.DataStack, 2);
    fixture.Functions[0x00E3]();
    AccessPointer stringPointer = GetAccessPointer(fixture);
    Assert(stringPointer.GetUl() == 2
        && fixture.McodePointer.GetUb(stringAddress + 8, 1)[0] == (byte)'B',
        "constant string array did not point to the selected string header");

    byte[] globalLayout = EncodeUInt32s(8, 2, 8, 3, 4, 0, 4);
    fixture = CreateDispatcher();
    uint globalTable = fixture.McodePointer.AppendBytes(globalLayout);
    WriteMcodeUInts(fixture, 0x70, globalTable);
    data = AttachDataPointer(fixture, 64);
    data.ResetDataIndex(24);
    data.SetUl(0xAABBCCDD);
    PushIntegerQuad(fixture.DataStack, 3);
    PushIntegerQuad(fixture.DataStack, 2);
    fixture.Functions[0x00E4]();
    Assert(data.GetUl() == 0xAABBCCDD, "global structure indexing selected the wrong field");

    fixture = CreateDispatcher();
    globalTable = fixture.McodePointer.AppendBytes(globalLayout);
    WriteMcodeUInts(fixture, 0x70, globalTable);
    PushIntegerQuad(fixture.DataStack, 3);
    PushIntegerQuad(fixture.DataStack, 2);
    fixture.Functions[0x00E5]();
    Assert(fixture.DataStack.PopUl() == 4 && fixture.DataStack.PopUl() == 0x80000018,
        "global structure record returned an incorrect address or size");

    byte[] localLayout = EncodeUInt32s(2, 4, 3, 4, 0, 4);
    foreach (ushort opcode in new ushort[] { 0x00E6, 0x00E7 })
    {
        fixture = CreateDispatcher();
        uint localTable = fixture.McodePointer.AppendBytes(localLayout);
        WriteMcodeUInts(fixture, 0x70, localTable);
        WriteMcodeUShorts(fixture, 0x74, 4);
        for (int i = 0; i < 6; i++) fixture.ReturnStack.PushUl(0);
        uint target = fixture.ReturnStack.GetCurrentStackPointer() * 4 + 16;
        fixture.ReturnStack.WriteAddress(target, 0xCAFEBABE, 4);
        PushIntegerQuad(fixture.DataStack, 3);
        PushIntegerQuad(fixture.DataStack, 2);
        fixture.Functions[opcode]();
        if (opcode == 0x00E6)
            Assert(GetAccessPointer(fixture).GetUl() == 0xCAFEBABE,
                "local structure indexing selected the wrong field");
        else
            Assert(fixture.DataStack.PopUl() == 4
                && fixture.DataStack.PopUl() == (0xC0000000 | target),
                "local structure record returned an incorrect address or size");
    }

    fixture = CreateDispatcher();
    data = AttachDataPointer(fixture, 32);
    data.ResetDataIndex(10);
    data.SetUb(0x5A);
    WriteMcodeUInts(fixture, 0x70, 2);
    fixture.DataStack.PushUl(0x80000008);
    fixture.DataStack.PushUl(4);
    fixture.Functions[0x00E8]();
    Assert(GetAccessPointer(fixture).GetUb() == 0x5A,
        "short temporary variable repoint used the wrong byte offset");

    fixture = CreateDispatcher();
    WriteMcodeUInts(fixture, 0x70, 2, 3);
    fixture.DataStack.PushUl(0x80000008);
    fixture.DataStack.PushUl(4);
    fixture.Functions[0x00E9]();
    Assert(fixture.DataStack.PopUl() == 3 && fixture.DataStack.PopUl() == 0x8000000A,
        "short temporary record repoint returned an incorrect address or size");

    byte[] temporaryLayout = EncodeUInt32s(4, 2, 4, 3, 4, 0, 4);
    fixture = CreateDispatcher();
    uint temporaryTable = fixture.McodePointer.AppendBytes(temporaryLayout);
    WriteMcodeUInts(fixture, 0x70, temporaryTable);
    data = AttachDataPointer(fixture, 32);
    data.ResetDataIndex(24);
    data.SetUl(0xDEADBEEF);
    fixture.DataStack.PushUl(0x80000008);
    fixture.DataStack.PushUl(4);
    PushIntegerQuad(fixture.DataStack, 3);
    PushIntegerQuad(fixture.DataStack, 2);
    fixture.Functions[0x00EA]();
    Assert(GetAccessPointer(fixture).GetUl() == 0xDEADBEEF,
        "indexed temporary variable repoint selected the wrong field");

    fixture = CreateDispatcher();
    temporaryTable = fixture.McodePointer.AppendBytes(temporaryLayout);
    WriteMcodeUInts(fixture, 0x70, temporaryTable);
    fixture.DataStack.PushUl(0x80000008);
    fixture.DataStack.PushUl(4);
    PushIntegerQuad(fixture.DataStack, 3);
    PushIntegerQuad(fixture.DataStack, 2);
    fixture.Functions[0x00EB]();
    Assert(fixture.DataStack.PopUl() == 4 && fixture.DataStack.PopUl() == 0x80000018,
        "indexed temporary record repoint returned an incorrect address or size");
}

static AccessPointer AttachDataPointer(DispatchFixture fixture, uint size)
{
    var pointer = new AccessPointer();
    pointer.SetupDataArray(size);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", pointer);
    return pointer;
}

static AccessPointer GetAccessPointer(DispatchFixture fixture) =>
    (AccessPointer)typeof(Dispatcher).GetField("ap", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(fixture.Dispatcher)!;

static void WriteMcodeUInts(DispatchFixture fixture, uint offset, params uint[] values) =>
    fixture.McodePointer.WriteBytes(offset, EncodeUInt32s(values));

static void WriteMcodeUShorts(DispatchFixture fixture, uint offset, params ushort[] values)
{
    byte[] bytes = new byte[checked(values.Length * 2)];
    for (int i = 0; i < values.Length; i++)
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2, 2), values[i]);
    fixture.McodePointer.WriteBytes(offset, bytes);
}

static byte[] EncodeUInt32s(params uint[] values)
{
    byte[] bytes = new byte[checked(values.Length * 4)];
    for (int i = 0; i < values.Length; i++)
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4, 4), values[i]);
    return bytes;
}

static byte[] CreateNumericArrayTable(uint width, ulong value)
{
    byte[] bytes = new byte[checked(2 + (int)(width * 3))];
    BinaryPrimitives.WriteUInt16LittleEndian(bytes, 3);
    for (uint index = 0; index < 3; index++)
        for (uint offset = 0; offset < width; offset++)
            bytes[checked(2 + (int)(index * width + offset))] = (byte)(value >> checked((int)(offset * 8)));
    return bytes;
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

    fixture = CreateDispatcher(0x0108, 5, 0x0017);
    fixture.DataStack.PushUl(3);
    fixture.DataStack.PushUl(0x12345678);
    Assert(fixture.Dispatch() == 0, "immediate subtraction failed");
    Assert(fixture.DataStack.PopUl() == 0x12345678 && unchecked((int)fixture.DataStack.PopUl()) == -2,
        "immediate subtraction changed the fractional word or lost its negative result");
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

static void TestConsoleIntegerConversions()
{
    foreach ((int dividend, int divisor, int remainder, int quotient) in new (int, int, int, int)[]
        { (17, 5, 2, 3), (-17, 5, -2, -3), (0, 5, 0, 0), (17, -5, 2, -3) })
    {
        DispatchFixture fixture = CreateDispatcher();
        fixture.DataStack.PushSl(dividend);
        fixture.DataStack.PushUl(0);
        fixture.DataStack.PushSl(divisor);
        fixture.DataStack.PushUl(0);
        fixture.Functions[0x010D]();
        Assert(fixture.DataStack.PopUl() == 0 && unchecked((int)fixture.DataStack.PopUl()) == quotient,
            "quotient order or sign differs");
        Assert(fixture.DataStack.PopUl() == 0 && unchecked((int)fixture.DataStack.PopUl()) == remainder,
            "remainder order or sign differs");
    }
    DispatchFixture zero = CreateDispatcher();
    zero.DataStack.PushUl(1);
    zero.DataStack.PushUl(0);
    zero.DataStack.PushUl(0);
    zero.DataStack.PushUl(0);
    try { zero.Functions[0x010D](); throw new InvalidOperationException("zero divisor was accepted"); }
    catch (DivideByZeroException) { }

    foreach (uint value in new uint[] { 0, 1, 0xFFFFFFFF })
    {
        DispatchFixture fixture = CreateDispatcher(0x0138, 0xFF, 0x0017);
        fixture.DataStack.PushUl(value);
        fixture.DataStack.PushUl(0);
        fixture.Functions[0x011C]();
        fixture.Functions[0x011A]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == unchecked(0u - value),
            "duplicate or negation changed the wrong word");
        fixture.Functions[0x0121]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == (value == 0xFFFFFFFF ? 1u : 0u),
            "negative predicate differs");
        fixture.DataStack.PushUl(value);
        fixture.DataStack.PushUl(0);
        Assert(fixture.Dispatch() == 0, "immediate AND failed");
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == (value & 0xFF),
            "immediate AND did not preserve the fractional word");
        fixture.DataStack.PushUl(value);
        fixture.DataStack.PushUl(0);
        fixture.DataStack.PushUl(0);
        fixture.DataStack.PushUl(0);
        fixture.Functions[0x0111]();
        fixture.DataStack.PushUl(0);
        fixture.DataStack.PushUl(0);
        fixture.Functions[0x00FE]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == (value == 0xFFFFFFFF ? 0u : 1u),
            "signed comparison or boolean OR differs");
    }
}

static void TestStringMargins()
{
    DispatchFixture fixture = CreateDispatcher();
    var data = new AccessPointer();
    data.SetupDataArray(40);
    data.SetUl4Ms(4, 0x80000010);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
    fixture.Functions[0x0182]();
    foreach (uint character in new uint[] { 0x82A0, 65, 66, 67 })
    {
        fixture.DataStack.PushUl(character);
        fixture.DataStack.PushUl(0);
        fixture.Functions[0x017A]();
    }
    Assert(data.GetBytes(16, 12).SequenceEqual(new byte[] { 4, 0, 0, 0, 0, 0, 0, 0, 66, 65, 0x82, 0xA0 }),
        "right margin or left append corrupted the string header or capacity");
    data.SetUl4Ms(4, 0x80000018);
    fixture.Functions[0x01A5]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 0x82A0
        && data.GetUl() == 2, "one-character extraction split Shift-JIS or returned string info instead of a character");
    fixture.DataStack.PushUl(0x80000018);
    fixture.DataStack.PushUl(2);
    fixture.Functions[0x01E9]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 2
        && fixture.DataStack.PopUl() == 2 && fixture.DataStack.PopUl() == 0x80000018,
        "byte count did not preserve the original string");
}

static void TestStringAppend()
{
    DispatchFixture fixture = CreateDispatcher();
    var data = new AccessPointer();
    data.SetupDataArray(32);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
    fixture.McodePointer.WriteBytes(0x70, BitConverter.GetBytes(4u));
    foreach (uint character in new uint[] { 65, 0x82A0 })
    {
        fixture.McodePointer.ResetMcodeIndex(0x70);
        fixture.DataStack.PushUl(character);
        fixture.DataStack.PushUl(0);
        fixture.Functions[0x0177]();
    }
    fixture.McodePointer.ResetMcodeIndex(0x70);
    fixture.Functions[0x0173]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 1, "append capacity differs");
    fixture.McodePointer.ResetMcodeIndex(0x70);
    fixture.DataStack.PushUl(0x80000009);
    fixture.DataStack.PushUl(2);
    fixture.Functions[0x017B]();
    Assert(data.GetUl() == 3, "append split a double-byte character to fill one remaining byte");
    fixture.McodePointer.ResetMcodeIndex(0x70);
    fixture.DataStack.PushUl(0x80000008);
    fixture.DataStack.PushUl(1);
    fixture.Functions[0x017B]();
    Assert(data.GetBytes(8, 4).SequenceEqual(new byte[] { 65, 0x82, 0xA0, 65 }) && data.GetUl() == 4,
        "overlapping append changed the source or stored length");
}

static void TestStringBoundaryAndXvarOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    WriteDataBytes(data, 64, (byte)'A', 0x82, 0xA0, (byte)'B');
    SetJinfo(data, 0x80000040, 4);
    PushIntegerQuad(fixture.DataStack, 3);
    fixture.Functions[0x0162]();
    Assert(ReadDataWord(data, 0) == 3 && ReadDataWord(data, 4) == 0x80000041
        && fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 1,
        "head limit did not delete complete characters or update the string reference");

    SetJinfo(data, 0x80000040, 4);
    PushIntegerQuad(fixture.DataStack, 3);
    fixture.Functions[0x0163]();
    Assert(ReadDataWord(data, 0) == 3 && ReadDataWord(data, 4) == 0x80000040
        && fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 1,
        "tail limit did not delete complete characters or preserve the original address");

    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 4);
    fixture.DataStack.PushUl(0x80000040);
    fixture.DataStack.PushUl(3);
    fixture.Functions[0x016B]();
    Assert(data.GetBytes(16, 4).SequenceEqual(new byte[] { (byte)'A', 0x82, 0xA0, 0x20 }),
        "Xvar pop did not preserve a complete character and space-pad the fixed-width target");

    WriteMcodeUInts(fixture, 0x70, 24, 3);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    fixture.DataStack.PushUl(0x80000040);
    fixture.DataStack.PushUl(4);
    fixture.Functions[0x016C]();
    Assert(data.GetBytes(24, 3).SequenceEqual(new byte[] { (byte)'A', 0x82, 0xA0 }),
        "Xvar data write split a multibyte character when truncating");

    fixture.ReturnStack.MoveStackPointer(-2);
    fixture.ReturnStack.WriteLocal(0, 0xCCCCCCCC, 4);
    WriteMcodeUShorts(fixture, 0x70, 0, 4);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    fixture.DataStack.PushUl(0x80000040);
    fixture.DataStack.PushUl(3);
    fixture.Functions[0x016D]();
    Assert(fixture.ReturnStack.ReadLocal(0, 4) == 0x20A08241,
        "local Xvar write did not place the fixed-width bytes in the return frame");

    WriteDataBytes(data, 80, 0xCC, 0xCC, 0xCC, 0xCC);
    fixture.DataStack.PushUl(0x80000050);
    fixture.DataStack.PushUl(4);
    fixture.Functions[0x016F]();
    Assert(data.GetBytes(80, 4).SequenceEqual(new byte[] { 0x20, 0x20, 0x20, 0x20 }),
        "simple string clear did not fill its target with spaces");

    uint stackPointer = fixture.DataStack.GetCurrentStackPointer();
    fixture.Functions[0x0172]();
    Assert(fixture.DataStack.GetCurrentStackPointer() == stackPointer,
        "reserved string-info opcode changed the data stack");
}

static void TestStringMarginOpcodeVariants()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 160);
    WriteStringRecord(data, 16, 2, 4, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 20);
    fixture.Functions[0x0175]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 4,
        "direct left-margin query returned the wrong reserve");
    SetJinfo(data, 0x80000010, 20);
    fixture.Functions[0x0174]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 14,
        "Jinfo right-margin query returned the wrong reserve");
    fixture.Functions[0x0176]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 4,
        "Jinfo left-margin query returned the wrong reserve");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 8);
    PushIntegerQuad(fixture.DataStack, 0x82A0);
    fixture.Functions[0x0177]();
    Assert(data.GetUl() == 4 && data.GetBytes(25, 4).SequenceEqual(new byte[] { (byte)'X', (byte)'Y', 0x82, 0xA0 }),
        "direct one-character right append did not preserve the character bytes");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    SetJinfo(data, 0x80000010, 8);
    PushIntegerQuad(fixture.DataStack, 0x82A0);
    fixture.Functions[0x0178]();
    data.ResetDataIndex(16);
    Assert(data.GetUl() == 4 && data.GetBytes(25, 4).SequenceEqual(new byte[] { (byte)'X', (byte)'Y', 0x82, 0xA0 }),
        "Jinfo one-character right append did not preserve the character bytes");

    WriteStringRecord(data, 16, 2, 2, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 8);
    PushIntegerQuad(fixture.DataStack, 'A');
    fixture.Functions[0x0179]();
    Assert(ReadDataWord(data, 16) == 3 && ReadDataWord(data, 20) == 1
        && data.GetBytes(25, 3).SequenceEqual(new byte[] { (byte)'A', (byte)'X', (byte)'Y' }),
        "direct one-character left append did not consume the left margin");

    WriteStringRecord(data, 16, 2, 2, (byte)'X', (byte)'Y');
    SetJinfo(data, 0x80000010, 8);
    fixture.DataStack.PushUl(0x80000060);
    fixture.DataStack.PushUl(3);
    WriteDataBytes(data, 96, (byte)'A', 0x82, 0xA0);
    data.ResetDataIndex(0);
    fixture.Functions[0x017C]();
    data.ResetDataIndex(16);
    Assert(data.GetUl() == 5 && data.GetBytes(26, 5).SequenceEqual(
        new byte[] { (byte)'X', (byte)'Y', (byte)'A', 0x82, 0xA0 }),
        "Jinfo string append did not preserve the complete source string");

    WriteStringRecord(data, 16, 2, 2, (byte)'X', (byte)'Y');
    WriteDataBytes(data, 96, (byte)'A', 0x82, 0xA0);
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 12);
    fixture.DataStack.PushUl(0x80000060);
    fixture.DataStack.PushUl(3);
    fixture.Functions[0x017D]();
    Assert(ReadDataWord(data, 16) == 4 && ReadDataWord(data, 20) == 0
        && data.GetBytes(24, 4).SequenceEqual(new byte[] { 0x82, 0xA0, (byte)'X', (byte)'Y' }),
        "direct left string append did not keep the fitting suffix on a character boundary");

    WriteStringRecord(data, 16, 2, 2, (byte)'X', (byte)'Y');
    SetJinfo(data, 0x80000010, 12);
    fixture.DataStack.PushUl(0x80000060);
    fixture.DataStack.PushUl(3);
    fixture.Functions[0x017E]();
    data.ResetDataIndex(16);
    Assert(ReadDataWord(data, 16) == 4 && ReadDataWord(data, 20) == 0
        && data.GetBytes(24, 4).SequenceEqual(new byte[] { 0x82, 0xA0, (byte)'X', (byte)'Y' }),
        "Jinfo left string append did not keep the fitting suffix on a character boundary");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 6);
    fixture.Functions[0x017F]();
    Assert(ReadDataWord(data, 16) == 6 && ReadDataWord(data, 20) == 0
        && data.GetBytes(24, 6).SequenceEqual(new byte[] { 0x20, (byte)'X', (byte)'Y', 0x20, 0x20, 0x20 }),
        "direct fill-space did not fill both margins or update the string length");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    SetJinfo(data, 0x80000010, 6);
    fixture.Functions[0x0180]();
    data.ResetDataIndex(16);
    Assert(ReadDataWord(data, 16) == 6 && ReadDataWord(data, 20) == 0
        && data.GetBytes(24, 6).SequenceEqual(new byte[] { 0x20, (byte)'X', (byte)'Y', 0x20, 0x20, 0x20 }),
        "Jinfo fill-space did not fill both margins or update the string length");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 7);
    fixture.Functions[0x0181]();
    Assert(ReadDataWord(data, 20) == 5 && data.GetBytes(29, 2).SequenceEqual(new byte[] { (byte)'X', (byte)'Y' }),
        "direct right alignment did not move the string to the right margin");
}

static void WriteDataBytes(AccessPointer data, uint offset, params byte[] bytes)
{
    for (uint index = 0; index < bytes.Length; index++)
    {
        data.ResetDataIndex(offset + index);
        data.SetUb(bytes[index]);
    }
}

static uint ReadDataWord(AccessPointer data, uint offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(data.GetBytes(offset, 4));

static void SetJinfo(AccessPointer data, uint address, uint capacity)
{
    data.ResetDataIndex(0);
    data.SetUl4Ms(capacity, address);
}

static void WriteStringRecord(AccessPointer data, uint offset, uint length, uint margin, params byte[] content)
{
    data.ResetDataIndex(offset);
    data.SetUl4Ms(length, margin);
    WriteDataBytes(data, offset + 8 + margin, content);
}

static void TestStringAlignmentAndMetadataOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 160);
    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 8);
    PushIntegerQuad(fixture.DataStack, 3);
    fixture.Functions[0x0183]();
    Assert(ReadDataWord(data, 20) == 4 && data.GetBytes(28, 2).SequenceEqual(new byte[] { (byte)'X', (byte)'Y' }),
        "partial right alignment moved by an incorrect number of bytes");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    SetJinfo(data, 0x80000010, 8);
    PushIntegerQuad(fixture.DataStack, 9);
    fixture.Functions[0x0184]();
    Assert(ReadDataWord(data, 20) == 6 && data.GetBytes(30, 2).SequenceEqual(new byte[] { (byte)'X', (byte)'Y' }),
        "Jinfo partial right alignment did not clamp the shift to available space");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 8);
    PushIntegerQuad(fixture.DataStack, 0);
    fixture.Functions[0x0183]();
    Assert(ReadDataWord(data, 20) == 1 && data.GetBytes(25, 2).SequenceEqual(new byte[] { (byte)'X', (byte)'Y' }),
        "zero partial-right shift changed the string");

    WriteStringRecord(data, 16, 2, 4, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 8);
    fixture.Functions[0x0185]();
    Assert(ReadDataWord(data, 20) == 0 && data.GetBytes(24, 2).SequenceEqual(new byte[] { (byte)'X', (byte)'Y' }),
        "full direct left alignment did not remove the complete left margin");

    WriteStringRecord(data, 16, 2, 3, (byte)'X', (byte)'Y');
    SetJinfo(data, 0x80000010, 8);
    fixture.Functions[0x0186]();
    Assert(ReadDataWord(data, 20) == 0 && data.GetBytes(24, 2).SequenceEqual(new byte[] { (byte)'X', (byte)'Y' }),
        "full Jinfo left alignment did not remove the complete left margin");

    WriteStringRecord(data, 16, 2, 4, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 8);
    PushIntegerQuad(fixture.DataStack, 1);
    fixture.Functions[0x0187]();
    Assert(ReadDataWord(data, 20) == 3 && data.GetBytes(27, 2).SequenceEqual(new byte[] { (byte)'X', (byte)'Y' }),
        "partial direct left alignment moved by an incorrect number of bytes");

    WriteStringRecord(data, 16, 2, 4, (byte)'X', (byte)'Y');
    SetJinfo(data, 0x80000010, 8);
    PushIntegerQuad(fixture.DataStack, 2);
    fixture.Functions[0x0188]();
    Assert(ReadDataWord(data, 20) == 2 && data.GetBytes(26, 2).SequenceEqual(new byte[] { (byte)'X', (byte)'Y' }),
        "partial Jinfo left alignment moved by an incorrect number of bytes");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 8);
    PushIntegerQuad(fixture.DataStack, 7);
    fixture.Functions[0x0189]();
    Assert(ReadDataWord(data, 16) == 7 && ReadDataWord(data, 20) == 1,
        "direct count setter wrote the wrong header word");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    SetJinfo(data, 0x80000010, 8);
    PushIntegerQuad(fixture.DataStack, 9);
    fixture.Functions[0x018A]();
    Assert(ReadDataWord(data, 16) == 9 && ReadDataWord(data, 20) == 1,
        "Jinfo count setter wrote the wrong header word");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 8);
    PushIntegerQuad(fixture.DataStack, 11);
    fixture.Functions[0x018B]();
    Assert(ReadDataWord(data, 16) == 2 && ReadDataWord(data, 20) == 11,
        "direct offset setter wrote the wrong header word");

    WriteStringRecord(data, 16, 2, 1, (byte)'X', (byte)'Y');
    SetJinfo(data, 0x80000010, 8);
    PushIntegerQuad(fixture.DataStack, 13);
    fixture.Functions[0x018C]();
    Assert(ReadDataWord(data, 16) == 2 && ReadDataWord(data, 20) == 13,
        "Jinfo offset setter wrote the wrong header word");
}

static void TestUppercaseStringOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] original = [
        (byte)'a', (byte)'Z', 0x82, 0x81, 0x82, 0x9A, 0x82, 0xA0, 0x83, 0x40
    ];
    byte[] expected = [
        (byte)'A', (byte)'Z', 0x82, 0x60, 0x82, 0x79, 0x82, 0xA0, 0x83, 0x40
    ];

    WriteStringRecord(data, 16, (uint)original.Length, 2, original);
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 32);
    fixture.Functions[0x018D]();
    Assert(ReadDataWord(data, 16) == original.Length && ReadDataWord(data, 20) == 2
        && data.GetBytes(26, (uint)expected.Length).SequenceEqual(expected),
        "direct uppercase conversion changed an unsupported character or the string header");

    WriteStringRecord(data, 16, (uint)original.Length, 2, original);
    SetJinfo(data, 0x80000010, 32);
    fixture.Functions[0x018E]();
    Assert(ReadDataWord(data, 16) == original.Length && ReadDataWord(data, 20) == 2
        && data.GetBytes(26, (uint)expected.Length).SequenceEqual(expected),
        "Jinfo uppercase conversion changed an unsupported character or the string header");

    WriteStringRecord(data, 16, 1, 0, 0x82);
    data.ResetDataIndex(16);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    WriteMcodeUInts(fixture, 0x70, 4);
    fixture.Functions[0x018D]();
    Assert(data.GetBytes(24, 1).SequenceEqual(new byte[] { 0x82 }),
        "uppercase conversion read beyond a truncated multibyte lead byte");
}

static void TestStringDeleteOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] leftText = [0x82, 0xA0, (byte)'B'];

    WriteStringRecord(data, 16, 3, 2, leftText);
    data.ResetDataIndex(16);
    fixture.Functions[0x018F]();
    Assert(ReadDataWord(data, 16) == 2 && ReadDataWord(data, 20) == 3,
        "Jvar leading-byte deletion did not advance the left margin");

    WriteDataBytes(data, 64, leftText);
    SetStringReference(data, 0x80000040, 3);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    fixture.Functions[0x0190]();
    Assert(ReadDataWord(data, 0) == 2 && ReadDataWord(data, 4) == 0x80000041,
        "Svar leading-byte deletion did not advance the source reference");

    WriteStringRecord(data, 16, 3, 2, leftText);
    SetJinfo(data, 0x80000010, 16);
    fixture.Functions[0x0191]();
    Assert(ReadDataWord(data, 16) == 2 && ReadDataWord(data, 20) == 3,
        "Jinfo leading-byte deletion did not advance the left margin");

    WriteStringRecord(data, 16, 3, 2, leftText);
    data.ResetDataIndex(16);
    fixture.Functions[0x0192]();
    Assert(ReadDataWord(data, 16) == 1 && ReadDataWord(data, 20) == 4,
        "Jvar leading-character deletion split a Shift-JIS character");

    WriteDataBytes(data, 64, leftText);
    SetStringReference(data, 0x80000040, 3);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    fixture.Functions[0x0193]();
    Assert(ReadDataWord(data, 0) == 1 && ReadDataWord(data, 4) == 0x80000042,
        "Svar leading-character deletion did not remove the complete Shift-JIS character");

    WriteStringRecord(data, 16, 3, 2, leftText);
    SetJinfo(data, 0x80000010, 16);
    fixture.Functions[0x0194]();
    Assert(ReadDataWord(data, 16) == 1 && ReadDataWord(data, 20) == 4,
        "Jinfo leading-character deletion did not remove the complete Shift-JIS character");

    byte[] rightText = [(byte)'A', 0x82, 0xA0];
    WriteStringRecord(data, 16, 3, 2, rightText);
    data.ResetDataIndex(16);
    fixture.Functions[0x0195]();
    Assert(ReadDataWord(data, 16) == 2 && ReadDataWord(data, 20) == 2,
        "Jvar trailing-byte deletion changed the left margin");

    WriteDataBytes(data, 64, rightText);
    SetStringReference(data, 0x80000040, 3);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    fixture.Functions[0x0196]();
    Assert(ReadDataWord(data, 0) == 2 && ReadDataWord(data, 4) == 0x80000040,
        "Svar trailing-byte deletion changed the source address");

    WriteStringRecord(data, 16, 3, 2, rightText);
    SetJinfo(data, 0x80000010, 16);
    fixture.Functions[0x0197]();
    Assert(ReadDataWord(data, 16) == 2 && ReadDataWord(data, 20) == 2,
        "Jinfo trailing-byte deletion changed the left margin");

    WriteStringRecord(data, 16, 3, 2, rightText);
    data.ResetDataIndex(16);
    fixture.Functions[0x0198]();
    Assert(ReadDataWord(data, 16) == 1 && ReadDataWord(data, 20) == 2,
        "Jvar trailing-character deletion split a Shift-JIS character");

    WriteDataBytes(data, 64, rightText);
    SetStringReference(data, 0x80000040, 3);
    fixture.McodePointer.ResetMcodeIndex(0x70);
    fixture.Functions[0x0199]();
    Assert(ReadDataWord(data, 0) == 1 && ReadDataWord(data, 4) == 0x80000040,
        "Svar trailing-character deletion changed its source address or split a character");

    WriteStringRecord(data, 16, 3, 2, rightText);
    SetJinfo(data, 0x80000010, 16);
    fixture.Functions[0x019A]();
    Assert(ReadDataWord(data, 16) == 1 && ReadDataWord(data, 20) == 2,
        "Jinfo trailing-character deletion changed the left margin or split a character");
}

static void TestStringCutOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] leftText = [0x82, 0xA0, (byte)'B'];
    byte[] rightText = [(byte)'A', 0x82, 0xA0];

    void AssertCutResult(uint expected, string context) =>
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"{context} returned the wrong character value");

    void RunRecord(uint opcode, bool jinfo, byte[] content, uint margin, uint expectedValue,
        uint expectedLength, uint expectedMargin)
    {
        WriteStringRecord(data, 16, (uint)content.Length, margin, content);
        if (jinfo)
            SetJinfo(data, 0x80000010, 32);
        else
            data.ResetDataIndex(16);
        fixture.McodePointer.ResetMcodeIndex(0x70);
        fixture.Functions[opcode]();
        AssertCutResult(expectedValue, $"opcode 0x{opcode:X4}");
        Assert(ReadDataWord(data, 16) == expectedLength && ReadDataWord(data, 20) == expectedMargin,
            $"opcode 0x{opcode:X4} updated the wrong string length or left margin");
    }

    void RunSvar(uint opcode, byte[] content, uint expectedValue, uint expectedAddress, uint expectedLength)
    {
        WriteDataBytes(data, 64, content);
        SetStringReference(data, 0x80000040, (uint)content.Length);
        fixture.McodePointer.ResetMcodeIndex(0x70);
        fixture.Functions[opcode]();
        AssertCutResult(expectedValue, $"opcode 0x{opcode:X4}");
        Assert(ReadDataWord(data, 0) == expectedLength && ReadDataWord(data, 4) == expectedAddress,
            $"opcode 0x{opcode:X4} updated the wrong Svar reference");
        Assert(fixture.McodePointer.GetMcodeIndex() == 0x70,
            $"opcode 0x{opcode:X4} consumed unexpected M-code");
    }

    RunRecord(0x019B, false, leftText, 2, 0x82, 2, 3);
    RunSvar(0x019C, leftText, 0x82, 0x80000041, 2);
    RunRecord(0x019D, true, leftText, 2, 0x82, 2, 3);
    RunRecord(0x019E, false, leftText, 2, 0x82A0, 1, 4);
    RunSvar(0x019F, leftText, 0x82A0, 0x80000042, 1);
    RunRecord(0x01A0, true, leftText, 2, 0x82A0, 1, 4);

    RunRecord(0x01A1, false, rightText, 2, 0xA0, 2, 2);
    RunSvar(0x01A2, rightText, 0xA0, 0x80000040, 2);
    RunRecord(0x01A3, true, rightText, 2, 0xA0, 2, 2);
    RunRecord(0x01A4, false, rightText, 2, 0x82A0, 1, 2);
    RunRecord(0x01A6, true, rightText, 2, 0x82A0, 1, 2);

    WriteStringRecord(data, 16, 0, 2);
    data.ResetDataIndex(16);
    fixture.Functions[0x019B]();
    AssertCutResult(0, "empty-string extraction");
    Assert(ReadDataWord(data, 16) == 0 && ReadDataWord(data, 20) == 2,
        "empty-string extraction modified the string record");
}

static void TestStringByteRangeOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] content = [(byte)'A', 0x82, 0xA0, (byte)'B'];

    void AssertStringInfo(uint expectedAddress, uint expectedLength, string context) =>
        Assert(fixture.DataStack.PopUl() == expectedLength && fixture.DataStack.PopUl() == expectedAddress,
            $"{context} returned the wrong string address or byte length");

    void RunRecord(uint opcode, bool jinfo, uint requested,
        uint expectedAddress, uint expectedCutLength, uint expectedLength, uint expectedMargin)
    {
        WriteStringRecord(data, 16, (uint)content.Length, 2, content);
        if (jinfo)
            SetJinfo(data, 0x80000010, 32);
        else
            data.ResetDataIndex(16);
        PushIntegerQuad(fixture.DataStack, requested);
        fixture.Functions[opcode]();
        AssertStringInfo(expectedAddress, expectedCutLength, $"opcode 0x{opcode:X4}");
        Assert(ReadDataWord(data, 16) == expectedLength && ReadDataWord(data, 20) == expectedMargin,
            $"opcode 0x{opcode:X4} updated the wrong string length or left margin");
    }

    void RunSvar(uint opcode, uint requested,
        uint expectedAddress, uint expectedCutLength, uint expectedLength, uint expectedReference)
    {
        WriteDataBytes(data, 64, content);
        SetStringReference(data, 0x80000040, (uint)content.Length);
        PushIntegerQuad(fixture.DataStack, requested);
        fixture.Functions[opcode]();
        AssertStringInfo(expectedAddress, expectedCutLength, $"opcode 0x{opcode:X4}");
        Assert(ReadDataWord(data, 0) == expectedLength && ReadDataWord(data, 4) == expectedReference,
            $"opcode 0x{opcode:X4} updated the wrong Svar reference");
    }

    void RunDeleteRecord(uint opcode, bool jinfo,
        uint expectedLength, uint expectedMargin)
    {
        WriteStringRecord(data, 16, (uint)content.Length, 2, content);
        if (jinfo)
            SetJinfo(data, 0x80000010, 32);
        else
            data.ResetDataIndex(16);
        PushIntegerQuad(fixture.DataStack, 2);
        fixture.Functions[opcode]();
        Assert(ReadDataWord(data, 16) == expectedLength && ReadDataWord(data, 20) == expectedMargin,
            $"opcode 0x{opcode:X4} updated the wrong string length or left margin");
    }

    void RunDeleteSvar(uint opcode, uint expectedLength, uint expectedAddress)
    {
        SetStringReference(data, 0x80000040, (uint)content.Length);
        PushIntegerQuad(fixture.DataStack, 2);
        fixture.Functions[opcode]();
        Assert(ReadDataWord(data, 0) == expectedLength && ReadDataWord(data, 4) == expectedAddress,
            $"opcode 0x{opcode:X4} updated the wrong Svar reference");
    }

    RunRecord(0x01A7, false, 2, 0x8000001A, 2, 2, 4);
    RunSvar(0x01A8, 2, 0x80000040, 2, 2, 0x80000042);
    RunRecord(0x01A9, true, 2, 0x8000001A, 2, 2, 4);
    RunRecord(0x01AA, false, 2, 0x8000001C, 2, 2, 2);
    RunSvar(0x01AB, 2, 0x80000042, 2, 2, 0x80000040);
    RunRecord(0x01AC, true, 2, 0x8000001C, 2, 2, 2);

    RunDeleteRecord(0x01AD, false, 2, 4);
    RunDeleteSvar(0x01AE, 2, 0x80000042);
    RunDeleteRecord(0x01AF, true, 2, 4);
    RunDeleteRecord(0x01B0, false, 2, 2);
    RunDeleteSvar(0x01B1, 2, 0x80000040);
    RunDeleteRecord(0x01B2, true, 2, 2);

    RunRecord(0x01A7, false, 99, 0x8000001A, 4, 0, 6);
}

static void TestStringCharacterRangeOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] content = [(byte)'A', 0x82, 0xA0, (byte)'B'];

    void AssertStringInfo(uint expectedAddress, uint expectedLength, string context) =>
        Assert(fixture.DataStack.PopUl() == expectedLength && fixture.DataStack.PopUl() == expectedAddress,
            $"{context} returned the wrong string address or byte length");

    void RunRecord(uint opcode, bool jinfo, uint requested,
        uint expectedAddress, uint expectedCutLength, uint expectedLength, uint expectedMargin)
    {
        WriteStringRecord(data, 16, (uint)content.Length, 2, content);
        if (jinfo)
            SetJinfo(data, 0x80000010, 32);
        else
            data.ResetDataIndex(16);
        PushIntegerQuad(fixture.DataStack, requested);
        fixture.Functions[opcode]();
        AssertStringInfo(expectedAddress, expectedCutLength, $"opcode 0x{opcode:X4}");
        Assert(ReadDataWord(data, 16) == expectedLength && ReadDataWord(data, 20) == expectedMargin,
            $"opcode 0x{opcode:X4} updated the wrong string length or left margin");
    }

    void RunSvar(uint opcode, uint requested,
        uint expectedAddress, uint expectedCutLength, uint expectedLength, uint expectedReference)
    {
        WriteDataBytes(data, 64, content);
        SetStringReference(data, 0x80000040, (uint)content.Length);
        PushIntegerQuad(fixture.DataStack, requested);
        fixture.Functions[opcode]();
        AssertStringInfo(expectedAddress, expectedCutLength, $"opcode 0x{opcode:X4}");
        Assert(ReadDataWord(data, 0) == expectedLength && ReadDataWord(data, 4) == expectedReference,
            $"opcode 0x{opcode:X4} updated the wrong Svar reference");
    }

    RunRecord(0x01B3, false, 2, 0x8000001A, 3, 1, 5);
    RunSvar(0x01B4, 2, 0x80000040, 3, 1, 0x80000043);
    RunRecord(0x01B5, true, 2, 0x8000001A, 3, 1, 5);
    RunRecord(0x01B6, false, 2, 0x8000001B, 3, 1, 2);
    RunSvar(0x01B7, 2, 0x80000041, 3, 1, 0x80000040);
    RunRecord(0x01B8, true, 2, 0x8000001B, 3, 1, 2);

    RunRecord(0x01B3, false, 0, 0x8000001A, 0, 4, 2);
}

static void TestStringSplitOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] content = [(byte)'A', 0x82, 0xA0, (byte)'B'];

    void AssertStringInfo(uint expectedAddress, uint expectedLength, string context) =>
        Assert(fixture.DataStack.PopUl() == expectedLength && fixture.DataStack.PopUl() == expectedAddress,
            $"{context} returned the wrong prefix address or byte length");

    void RunRecord(uint opcode, bool jinfo, uint position,
        uint expectedAddress, uint expectedPrefixLength, uint expectedLength, uint expectedMargin)
    {
        WriteStringRecord(data, 16, (uint)content.Length, 2, content);
        if (jinfo)
            SetJinfo(data, 0x80000010, 32);
        else
            data.ResetDataIndex(16);
        PushIntegerQuad(fixture.DataStack, position);
        fixture.Functions[opcode]();
        AssertStringInfo(expectedAddress, expectedPrefixLength, $"opcode 0x{opcode:X4}");
        Assert(ReadDataWord(data, 16) == expectedLength && ReadDataWord(data, 20) == expectedMargin,
            $"opcode 0x{opcode:X4} removed the wrong prefix or delimiter");
    }

    void RunSvar(uint opcode, uint position,
        uint expectedAddress, uint expectedPrefixLength, uint expectedLength, uint expectedReference)
    {
        WriteDataBytes(data, 64, content);
        SetStringReference(data, 0x80000040, (uint)content.Length);
        PushIntegerQuad(fixture.DataStack, position);
        fixture.Functions[opcode]();
        AssertStringInfo(expectedAddress, expectedPrefixLength, $"opcode 0x{opcode:X4}");
        Assert(ReadDataWord(data, 0) == expectedLength && ReadDataWord(data, 4) == expectedReference,
            $"opcode 0x{opcode:X4} updated the wrong Svar reference");
    }

    RunRecord(0x01B9, false, 2, 0x8000001A, 1, 1, 5);
    RunSvar(0x01BA, 2, 0x80000040, 1, 1, 0x80000043);
    RunRecord(0x01BB, true, 1, 0x8000001A, 0, 3, 3);
    RunRecord(0x01BC, false, 3, 0x8000001A, 2, 1, 5);
    RunSvar(0x01BD, 3, 0x80000040, 2, 1, 0x80000043);
    RunRecord(0x01BE, true, 0, 0x8000001A, 0, 4, 2);
}

static void TestStringCharacterRangeDeleteOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] content = [(byte)'A', 0x82, 0xA0, (byte)'B'];

    void RunRecord(uint opcode, bool jinfo, uint expectedLength, uint expectedMargin, uint requested = 2)
    {
        WriteStringRecord(data, 16, (uint)content.Length, 2, content);
        if (jinfo)
            SetJinfo(data, 0x80000010, 32);
        else
            data.ResetDataIndex(16);
        uint stackPointer = fixture.DataStack.GetCurrentStackPointer();
        PushIntegerQuad(fixture.DataStack, requested);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.GetCurrentStackPointer() == stackPointer,
            $"opcode 0x{opcode:X4} left unexpected data on the stack");
        Assert(ReadDataWord(data, 16) == expectedLength && ReadDataWord(data, 20) == expectedMargin,
            $"opcode 0x{opcode:X4} updated the wrong string length or left margin");
    }

    void RunSvar(uint opcode, uint expectedLength, uint expectedAddress)
    {
        WriteDataBytes(data, 64, content);
        SetStringReference(data, 0x80000040, (uint)content.Length);
        uint stackPointer = fixture.DataStack.GetCurrentStackPointer();
        PushIntegerQuad(fixture.DataStack, 2);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.GetCurrentStackPointer() == stackPointer,
            $"opcode 0x{opcode:X4} left unexpected data on the stack");
        uint actualLength = ReadDataWord(data, 0);
        uint actualAddress = ReadDataWord(data, 4);
        Assert(actualLength == expectedLength && actualAddress == expectedAddress,
            $"opcode 0x{opcode:X4} expected Svar ({expectedLength}, 0x{expectedAddress:X8}) but got ({actualLength}, 0x{actualAddress:X8})");
    }

    RunRecord(0x01BF, false, 1, 5);
    RunSvar(0x01C0, 1, 0x80000043);
    RunRecord(0x01C1, true, 1, 5);
    RunRecord(0x01C2, false, 1, 2);
    RunSvar(0x01C3, 1, 0x80000040);
    RunRecord(0x01C4, true, 1, 2);
    RunRecord(0x01BF, false, 0, 6, 99);
}

static void TestStringWhitespaceOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] content = [0x20, 0x09, 0x00, 0x81, 0x40, 0x81, 0x43, 0x81, 0x41, 0x2C,
        (byte)'X', 0x81, 0x40, 0x2C, 0x20];

    void RunRecord(uint opcode, bool jinfo, uint expectedLength, uint expectedMargin)
    {
        WriteStringRecord(data, 16, (uint)content.Length, 2, content);
        if (jinfo)
            SetJinfo(data, 0x80000010, 32);
        else
            data.ResetDataIndex(16);
        fixture.Functions[opcode]();
        Assert(ReadDataWord(data, 16) == expectedLength && ReadDataWord(data, 20) == expectedMargin,
            $"opcode 0x{opcode:X4} trimmed the wrong whitespace or changed the wrong margin");
    }

    void RunSvar(uint opcode, uint expectedLength, uint expectedAddress)
    {
        WriteDataBytes(data, 64, content);
        SetStringReference(data, 0x80000040, (uint)content.Length);
        fixture.Functions[opcode]();
        Assert(ReadDataWord(data, 0) == expectedLength && ReadDataWord(data, 4) == expectedAddress,
            $"opcode 0x{opcode:X4} updated the wrong Svar reference");
    }

    RunRecord(0x01C5, false, 5, 12);
    RunSvar(0x01C6, 5, 0x8000004A);
    RunRecord(0x01C7, true, 5, 12);
    RunRecord(0x01C8, false, 11, 2);
    RunSvar(0x01C9, 11, 0x80000040);
    RunRecord(0x01CA, true, 11, 2);
}

static void TestStringWordExtractionOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] content = [(byte)' ', 0x81, 0x40, (byte)'A', 0x82, 0xA0, 0x2C, (byte)'B', (byte)' '];

    void AssertWord(uint expectedAddress, uint expectedLength, string context) =>
        Assert(fixture.DataStack.PopUl() == expectedLength && fixture.DataStack.PopUl() == expectedAddress,
            $"{context} returned the wrong word address or byte length");

    void RunRecord(uint opcode, bool jinfo)
    {
        WriteStringRecord(data, 16, (uint)content.Length, 2, content);
        if (jinfo)
            SetJinfo(data, 0x80000010, 32);
        else
            data.ResetDataIndex(16);
        fixture.Functions[opcode]();
        AssertWord(0x8000001D, 3, $"opcode 0x{opcode:X4}");
        Assert(ReadDataWord(data, 16) == 3 && ReadDataWord(data, 20) == 8,
            $"opcode 0x{opcode:X4} did not remove the leading whitespace and one word");
    }

    void RunSvar()
    {
        WriteDataBytes(data, 64, content);
        SetStringReference(data, 0x80000040, (uint)content.Length);
        fixture.Functions[0x01CC]();
        AssertWord(0x80000043, 3, "direct Svar word extraction");
        Assert(ReadDataWord(data, 0) == 3 && ReadDataWord(data, 4) == 0x80000046,
            "direct Svar word extraction updated the wrong remaining reference");
    }

    RunRecord(0x01CB, false);
    RunSvar();
    RunRecord(0x01CD, true);

    WriteStringRecord(data, 16, 4, 2, 0x20, 0x2C, 0x81, 0x40);
    data.ResetDataIndex(16);
    fixture.Functions[0x01CB]();
    AssertWord(0x8000001E, 0, "all-whitespace word extraction");
    Assert(ReadDataWord(data, 16) == 0 && ReadDataWord(data, 20) == 6,
        "all-whitespace word extraction did not consume the full string");
}

static void TestStringCharacterSelectionOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    const uint address = 0x80000040;
    byte[] content = [(byte)'A', 0x82, 0xA0, (byte)'B', 0x81, 0x40];
    WriteDataBytes(data, 64, content);

    void PushString(uint length)
    {
        fixture.DataStack.PushUl(address);
        fixture.DataStack.PushUl(length);
    }

    void AssertQuad(uint opcode, uint expected, string context)
    {
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"{context} returned the wrong value");
    }

    PushString((uint)content.Length);
    AssertQuad(0x01CE, 4, "character count");
    PushString((uint)content.Length);
    AssertQuad(0x01CF, (byte)'A', "first byte");
    PushString((uint)content.Length);
    AssertQuad(0x01D0, (byte)'A', "first character");
    PushString((uint)content.Length);
    AssertQuad(0x01D1, 0x40, "last byte");
    PushString((uint)content.Length);
    AssertQuad(0x01D2, 0x8140, "last character");

    void PushPosition(uint position)
    {
        PushString((uint)content.Length);
        fixture.DataStack.PushUl(position);
        fixture.DataStack.PushUl(0);
    }

    PushPosition(2);
    AssertQuad(0x01D3, 0x82A0, "second character from the left");
    PushPosition(2);
    AssertQuad(0x01D4, (byte)'B', "second character from the right");
    PushPosition(0);
    AssertQuad(0x01D3, 0, "zero character position");
    PushPosition(5);
    AssertQuad(0x01D4, 0, "out-of-range character position");

    void PushRange(uint requested)
    {
        PushString((uint)content.Length);
        fixture.DataStack.PushUl(requested);
        fixture.DataStack.PushUl(0);
    }

    PushRange(2);
    fixture.Functions[0x01D5]();
    Assert(fixture.DataStack.PopUl() == 3 && fixture.DataStack.PopUl() == address,
        "left extraction returned the wrong character range");
    PushRange(2);
    fixture.Functions[0x01D6]();
    Assert(fixture.DataStack.PopUl() == 3 && fixture.DataStack.PopUl() == address + 3,
        "right extraction returned the wrong character range");

    PushString(0);
    AssertQuad(0x01CE, 0, "empty character count");
    PushString(0);
    AssertQuad(0x01D0, 0, "empty first character");
    PushString(0);
    fixture.DataStack.PushUl(2);
    fixture.DataStack.PushUl(0);
    fixture.Functions[0x01D6]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == address,
        "right extraction from an empty string returned a nonempty range");
}

static void TestStringSearchAndComparisonOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    const uint orgAddress = 0x80000040;
    const uint keyAddress = 0x80000050;
    byte[] org = [(byte)'A', (byte)'X', (byte)'A', (byte)'B', 0x82, 0xA0, (byte)'C'];
    byte[] key = [(byte)'A', (byte)'B'];
    WriteDataBytes(data, 64, org);
    WriteDataBytes(data, 80, key);

    void PushString(uint address, uint length)
    {
        fixture.DataStack.PushUl(address);
        fixture.DataStack.PushUl(length);
    }

    void AssertQuad(uint opcode, uint expected, string context)
    {
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"{context} returned the wrong comparison or position");
    }

    PushString(orgAddress, (uint)org.Length);
    fixture.DataStack.PushUl(0x82A0);
    fixture.DataStack.PushUl(0);
    AssertQuad(0x01D9, 5, "Shift-JIS character search");
    PushString(orgAddress, (uint)org.Length);
    fixture.DataStack.PushUl((byte)'X');
    fixture.DataStack.PushUl(0);
    AssertQuad(0x01D9, 2, "ASCII character search");

    PushString(orgAddress, (uint)org.Length);
    PushString(keyAddress, (uint)key.Length);
    fixture.Functions[0x01DA]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 3,
        "character-aware substring search returned the wrong position");
    fixture.Functions[0x01DB]();
    Assert(fixture.DataStack.PopUl() == 3 && fixture.DataStack.PopUl() == orgAddress + 4,
        "remaining-string search result has the wrong address or length");

    WriteDataBytes(data, 80, (byte)0xA0, (byte)'C');
    PushString(orgAddress, (uint)org.Length);
    PushString(keyAddress, 2);
    fixture.Functions[0x01DC]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 6,
        "byte substring search did not match across a Shift-JIS boundary");

    PushString(orgAddress, 1);
    PushString(keyAddress, 2);
    fixture.Functions[0x01DA]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 0,
        "failed substring search returned a nonzero position");
    fixture.Functions[0x01DB]();
    Assert(fixture.DataStack.PopUl() == 3 && fixture.DataStack.PopUl() == orgAddress + 4,
        "failed search incorrectly replaced the previous remaining-string result");

    WriteDataBytes(data, 64, (byte)'A');
    WriteDataBytes(data, 80, (byte)'B');
    PushString(orgAddress, 1);
    PushString(keyAddress, 1);
    AssertQuad(0x01DE, 1, "bytewise string comparison");
    WriteDataBytes(data, 64, (byte)'Z');
    WriteDataBytes(data, 80, 0x81, 0x40);
    PushString(orgAddress, 1);
    PushString(keyAddress, 2);
    AssertQuad(0x01DF, 1, "characterwise string comparison");

    byte[] filenameA = System.Text.Encoding.ASCII.GetBytes("Dir/file.TXT");
    byte[] filenameB = System.Text.Encoding.ASCII.GetBytes("dir/FILE.txt");
    WriteDataBytes(data, 64, filenameA);
    WriteDataBytes(data, 80, filenameB);
    PushString(orgAddress, (uint)filenameA.Length);
    PushString(keyAddress, (uint)filenameB.Length);
    AssertQuad(0x01E0, 0, "case-insensitive filename comparison");
}

static void TestCharacterClassificationOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    void AssertQuad(uint opcode, uint input, uint expected, string context)
    {
        fixture.DataStack.PushUl(input);
        fixture.DataStack.PushUl(0xABCD);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0xABCD && fixture.DataStack.PopUl() == expected,
            $"{context} returned the wrong value or changed the upper quad word");
    }

    AssertQuad(0x01E1, 0x82, 1, "Shift-JIS lead byte");
    AssertQuad(0x01E1, 0x41, 0, "Shift-JIS ASCII byte");
    AssertQuad(0x01E2, 0xA1, 1, "EUC high-bit byte");
    AssertQuad(0x01E2, 0x41, 0, "EUC ASCII byte");
    AssertQuad(0x01E3, 0x82, 1, "configured lead byte");
    AssertQuad(0x01E3, 0xA1, 0, "configured non-lead byte");
    AssertQuad(0x01E4, 0x8140, 1, "double-byte character code");
    AssertQuad(0x01E4, 0xFF, 0, "single-byte character code");
    AssertQuad(0x01E4, 0x10041, 0, "character code truncates to 16 bits");
    AssertQuad(0x01E5, 0xA1, 1, "half-width kana code");
    AssertQuad(0x01E5, 0xE0, 0, "non-kana code");
    AssertQuad(0x01E6, 0xA1, 1, "character width of single-byte kana");
    AssertQuad(0x01E6, 0x8140, 2, "character width of double-byte code");
    AssertQuad(0x01E6, 0x10041, 1, "character width truncates to 16 bits");
    AssertQuad(0x01E7, 0xA1, 1, "display width of single-byte kana");
    AssertQuad(0x01E7, 0x8140, 2, "display width of double-byte code");
    AssertQuad(0x01E8, 0x09, 1, "ASCII whitespace");
    AssertQuad(0x01E8, 0x2C, 1, "ASCII comma whitespace");
    AssertQuad(0x01E8, 0x8140, 1, "Shift-JIS whitespace");
    AssertQuad(0x01E8, 0x8142, 0, "non-whitespace double-byte code");
    AssertQuad(0x01E8, 0x10020, 1, "whitespace check truncates to 16 bits");
}

static void TestStringSizeOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    void RunLengthAlias(uint opcode)
    {
        fixture.DataStack.PushUl(0x80000040);
        fixture.DataStack.PushUl(7);
        uint pointer = fixture.DataStack.GetCurrentStackPointer();
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.GetCurrentStackPointer() == pointer,
            $"opcode 0x{opcode:X4} changed the stack depth");
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 7,
            $"opcode 0x{opcode:X4} did not replace string info with its byte count");
    }

    RunLengthAlias(0x01EA);
    RunLengthAlias(0x01EB);

    fixture.DataStack.PushUl(0x80000040);
    fixture.DataStack.PushUl(7);
    fixture.DataStack.PushUl(10);
    fixture.DataStack.PushUl(0);
    fixture.Functions[0x01ED]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 0x80000040,
        "size reduction above the string length produced an invalid string reference");

    fixture.DataStack.PushUl(0x80000040);
    fixture.DataStack.PushUl(7);
    fixture.DataStack.PushUl(3);
    fixture.DataStack.PushUl(0);
    fixture.Functions[0x01ED]();
    Assert(fixture.DataStack.PopUl() == 4 && fixture.DataStack.PopUl() == 0x80000040,
        "size reduction did not subtract the requested byte count");
}

static void TestSingleCharacterConversionOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    void PushCode(uint code)
    {
        fixture.DataStack.PushUl(code);
        fixture.DataStack.PushUl(0xABCD);
    }

    void AssertChanged(uint opcode, uint input, uint expected, string context)
    {
        PushCode(input);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0xABCD && fixture.DataStack.PopUl() == expected,
            $"{context} changed the wrong code or upper quad word");
    }

    AssertChanged(0x01EE, 0x8140, 0x2121, "Shift-JIS to JIS conversion");
    AssertChanged(0x01EF, 0x2121, 0x8140, "JIS to Shift-JIS conversion");
    AssertChanged(0x01F0, (byte)'z', (byte)'Z', "ASCII uppercase conversion");
    AssertChanged(0x01F0, 0x8281, 0x8260, "Shift-JIS uppercase conversion");
    AssertChanged(0x01F3, (byte)'A', 0x8260, "ASCII full-width conversion");
    AssertChanged(0x01F3, (byte)' ', 0x8140, "ASCII space full-width conversion");
    AssertChanged(0x01F3, 0xA6, 0x8392, "half-width kana conversion");
    AssertChanged(0x01F3, 0xDF, 0x814B, "half-width punctuation conversion");
    AssertChanged(0x01F3, 0xE0, 0xE0, "non-mapped byte conversion");
    AssertChanged(0x01F3, 0x8140, 0x8140, "existing full-width character conversion");

    void AssertNumeric(uint opcode, uint input, uint expected, uint success, string context)
    {
        PushCode(input);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == success,
            $"{context} returned the wrong success flag");
        Assert(fixture.DataStack.PopUl() == 0xABCD && fixture.DataStack.PopUl() == expected,
            $"{context} returned the wrong value or changed the upper quad word");
    }

    AssertNumeric(0x01F1, (byte)'7', 7, 1, "decimal digit conversion");
    AssertNumeric(0x01F1, 0x8256, 7, 1, "full-width decimal digit conversion");
    AssertNumeric(0x01F1, (byte)'A', 0, 0, "invalid decimal conversion");
    AssertNumeric(0x01F2, (byte)'F', 15, 1, "hexadecimal conversion");
    AssertNumeric(0x01F2, 0x8286, 15, 1, "full-width hexadecimal conversion");
    AssertNumeric(0x01F2, (byte)'G', 0, 0, "invalid hexadecimal conversion");

    AssertChanged(0x01F4, (byte)'(', 1, "ASCII separator");
    AssertChanged(0x01F4, 0x8169, 1, "Shift-JIS separator");
    AssertChanged(0x01F4, 0x8142, 1, "Shift-JIS comma separator");
    AssertChanged(0x01F4, (byte)'X', 0, "ordinary character separator check");
}

static void TestSvarStateAndDeletionOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);

    void PushSvar(uint address, uint length)
    {
        fixture.DataStack.PushUl(address);
        fixture.DataStack.PushUl(length);
    }

    void AssertEmptyCheck(uint opcode, uint length, uint expected, string context)
    {
        PushSvar(0x80000040, length);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"{context} returned the wrong boolean quad");
        Assert(fixture.DataStack.PopUl() == length && fixture.DataStack.PopUl() == 0x80000040,
            $"{context} changed its input string info");
    }

    AssertEmptyCheck(0x01F5, 0, 1, "empty Svar check");
    AssertEmptyCheck(0x01F5, 2, 0, "non-empty Svar empty check");
    AssertEmptyCheck(0x01F6, 0, 0, "empty Svar non-empty check");
    AssertEmptyCheck(0x01F6, 2, 1, "non-empty Svar check");

    void AssertDeletion(uint opcode, byte[] content, uint expectedAddress, uint expectedLength, string context)
    {
        WriteDataBytes(data, 64, content);
        fixture.DataStack.PushUl(0xCAFEBABE);
        PushSvar(0x80000040, (uint)content.Length);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == expectedLength && fixture.DataStack.PopUl() == expectedAddress,
            $"{context} changed the wrong string-info words");
        Assert(fixture.DataStack.PopUl() == 0xCAFEBABE,
            $"{context} changed the adjacent stack value");
    }

    byte[] content = [(byte)'A', 0x82, 0xA0, (byte)'B'];
    AssertDeletion(0x01F7, content, 0x80000041, 3, "left byte deletion");
    AssertDeletion(0x01F8, content, 0x80000040, 3, "right byte deletion");
    AssertDeletion(0x01F9, [0x82, 0xA0, (byte)'A', (byte)'B'], 0x80000042, 2,
        "left multibyte character deletion");
    AssertDeletion(0x01FA, content, 0x80000040, 3, "right character deletion");
    AssertDeletion(0x01F9, [], 0x80000040, 0, "empty character deletion");
    AssertDeletion(0x01FA, [(byte)'A', 0x82, 0xA0], 0x80000040, 1,
        "right multibyte character deletion");
}

static void TestSvarExtractionOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);

    void AssertCut(uint opcode, byte[] content, uint expectedValue, uint expectedAddress,
        uint expectedLength, string context)
    {
        WriteDataBytes(data, 64, content);
        fixture.DataStack.PushUl(0xCAFEBABE);
        fixture.DataStack.PushUl(0x80000040);
        fixture.DataStack.PushUl((uint)content.Length);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expectedValue,
            $"{context} returned the wrong character quad");
        Assert(fixture.DataStack.PopUl() == expectedLength && fixture.DataStack.PopUl() == expectedAddress,
            $"{context} updated the wrong remaining string info");
        Assert(fixture.DataStack.PopUl() == 0xCAFEBABE,
            $"{context} changed the adjacent stack value");
    }

    AssertCut(0x01FB, [0x82, 0xA0, (byte)'B'], 0x82, 0x80000041, 2,
        "first byte extraction");
    AssertCut(0x01FC, [0x82, 0xA0, (byte)'B'], (byte)'B', 0x80000040, 2,
        "last byte extraction");
    AssertCut(0x01FD, [0x82, 0xA0, (byte)'B'], 0x82A0, 0x80000042, 1,
        "first multibyte character extraction");
    AssertCut(0x01FE, [(byte)'A', 0x82, 0xA0], 0x82A0, 0x80000040, 1,
        "last multibyte character extraction");
    AssertCut(0x01FB, [], 0, 0x80000040, 0, "empty first byte extraction");
    AssertCut(0x01FE, [], 0, 0x80000040, 0, "empty last character extraction");
}

static void TestSvarByteRangeOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    AccessPointer data = AttachDataPointer(fixture, 128);
    byte[] content = [(byte)'A', 0x82, 0xA0, (byte)'B'];

    void AssertCut(uint opcode, uint requested, uint expectedResultAddress, uint expectedResultLength,
        uint expectedAddress, uint expectedLength, string context)
    {
        WriteDataBytes(data, 64, content);
        fixture.DataStack.PushUl(0xCAFEBABE);
        fixture.DataStack.PushUl(0x80000040);
        fixture.DataStack.PushUl((uint)content.Length);
        fixture.DataStack.PushUl(requested);
        fixture.DataStack.PushUl(0);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == expectedResultLength
            && fixture.DataStack.PopUl() == expectedResultAddress,
            $"{context} returned the wrong extracted byte range");
        Assert(fixture.DataStack.PopUl() == expectedLength
            && fixture.DataStack.PopUl() == expectedAddress,
            $"{context} updated the wrong remaining string info");
        Assert(fixture.DataStack.PopUl() == 0xCAFEBABE,
            $"{context} changed the adjacent stack value");
    }

    void AssertDelete(uint opcode, uint requested, uint expectedAddress, uint expectedLength, string context)
    {
        WriteDataBytes(data, 64, content);
        fixture.DataStack.PushUl(0xCAFEBABE);
        fixture.DataStack.PushUl(0x80000040);
        fixture.DataStack.PushUl((uint)content.Length);
        fixture.DataStack.PushUl(requested);
        fixture.DataStack.PushUl(0);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == expectedLength && fixture.DataStack.PopUl() == expectedAddress,
            $"{context} updated the wrong string info");
        Assert(fixture.DataStack.PopUl() == 0xCAFEBABE,
            $"{context} changed the adjacent stack value");
    }

    AssertCut(0x01FF, 2, 0x80000040, 2, 0x80000042, 2, "left byte-range extraction");
    AssertCut(0x0200, 2, 0x80000042, 2, 0x80000040, 2, "right byte-range extraction");
    AssertCut(0x01FF, 99, 0x80000040, 4, 0x80000044, 0, "clamped left byte-range extraction");
    AssertCut(0x0200, 99, 0x80000040, 4, 0x80000040, 0, "clamped right byte-range extraction");
    AssertDelete(0x0201, 2, 0x80000042, 2, "left byte-range deletion");
    AssertDelete(0x0202, 2, 0x80000040, 2, "right byte-range deletion");
    AssertDelete(0x0201, 99, 0x80000044, 0, "clamped left byte-range deletion");
    AssertDelete(0x0202, 99, 0x80000040, 0, "clamped right byte-range deletion");
}

static void SetStringReference(AccessPointer data, uint address, uint length)
{
    data.ResetDataIndex(0);
    data.SetUl4Ms(length, address);
}

static void TestCountedLoops()
{
    foreach (uint count in new uint[] { 0, 1, 3 })
    {
        DispatchFixture fixture = CreateDispatcher(0x00EE, 8, 0x00F1, 0x00EF, 0xFFFC, 0x0017);
        fixture.ReturnStack.PushUl(0x12345678);
        fixture.DataStack.PushUl(count);
        fixture.DataStack.PushUl(0);
        Assert(fixture.Dispatch() == 0, "counted loop dispatch failed");
        for (uint expected = count; expected > 0; expected--)
            Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
                "counted loop did not run from one through its bound");
        Assert(fixture.DataStack.GetCurrentStackPointer() == fixture.InitialDataStackPointer,
            "zero-count loop ran its body or loop left stack data");
        Assert(fixture.ReturnStack.PopUl() == 0x12345678
            && fixture.ReturnStack.GetCurrentStackPointer() == fixture.InitialReturnStackPointer,
            "counted loop changed the return frame");
    }

    DispatchFixture nested = CreateDispatcher();
    nested.ReturnStack.PushUl(0x12345678);
    foreach (uint bound in new uint[] { 3, 2 })
    {
        nested.McodePointer.ResetMcodeIndex(0x70);
        nested.DataStack.PushUl(bound);
        nested.DataStack.PushUl(0);
        nested.Functions[0x00EE]();
    }
    nested.McodePointer.ResetMcodeIndex(0x70);
    nested.McodePointer.WriteBytes(0x70, BitConverter.GetBytes((short)4));
    nested.Functions[0x00F0]();
    Assert(nested.McodePointer.GetMcodeIndex() == 0x74 && nested.ReturnStack.GetUl() == 1
        && nested.ReturnStack.GetUlOffset(1) == 3 && nested.ReturnStack.GetUlOffset(2) == 0x12345678,
        "breaking the inner loop discarded the outer loop or its return frame");
}

static void TestDoubleArrayAccess()
{
    foreach (uint index in new uint[] { 1, 3 })
    {
        DispatchFixture fixture = CreateDispatcher(0x00D5, 4, 0, 3, 0, 0x0017);
        var data = new AccessPointer();
        data.SetupDataArray(20);
        data.SetUb4Msi(0xCC, 20);
        SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
        fixture.DataStack.PushUl(index);
        fixture.DataStack.PushUl(0);
        Assert(fixture.Dispatch() == 0, "double array selection failed");
        fixture.DataStack.PushUl(0x12345678);
        fixture.DataStack.PushUl(0);
        fixture.Functions[0x005C]();
        fixture.Functions[0x0047]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 0x12345678,
            "double array value changed");
        int offset = (int)index * 4;
        byte[] bytes = data.GetBytes(0, 20);
        Assert(bytes.Where((_, i) => i < offset || i >= offset + 4).All(value => value == 0xCC),
            "double array write changed an adjacent element");
    }
    foreach (uint index in new uint[] { 0, 4 })
    {
        DispatchFixture fixture = CreateDispatcher(0x00D5, 4, 0, 3, 0);
        fixture.DataStack.PushUl(index);
        fixture.DataStack.PushUl(0);
        try { fixture.Dispatch(); }
        catch (TargetInvocationException exception) when (exception.InnerException is IndexOutOfRangeException) { continue; }
        throw new InvalidOperationException("out-of-range double array element was accepted");
    }
}

static void TestCharacterStackStrings()
{
    foreach ((uint character, byte[] expected) in new (uint, byte[])[]
        { (65, new byte[] { 65 }), (0x82A0, new byte[] { 0x82, 0xA0 }) })
    {
        DispatchFixture fixture = CreateDispatcher();
        fixture.DataStack.PushUl(character);
        fixture.DataStack.PushUl(0);
        fixture.Functions[0x0161]();
        Assert(fixture.DataStack.PopUl() == expected.Length, "character string length differs");
        uint address = fixture.DataStack.PopUl();
        uint pointer = fixture.DataStack.GetCurrentStackPointer();
        fixture.DataStack.PushUl(123);
        fixture.DataStack.PushUl(456);
        for (int i = 0; i < expected.Length; i++)
        {
            fixture.DataStack.PushUl(address + (uint)i);
            fixture.DataStack.PushUl(0);
            fixture.Functions[0x008F]();
            Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected[i],
                "character string did not reference the original stack bytes");
        }
        fixture.DataStack.MoveStackPointer(2);
        Assert(fixture.DataStack.GetCurrentStackPointer() == pointer, "character conversion lost the original value");
        Assert(fixture.DataStack.GetUlOffset(1) == (expected.Length == 1 ? character : 0xA082),
            "little-endian character conversion differs from the C runtime");
    }
}

static void TestLocalStringAddresses()
{
    DispatchFixture fixture = CreateDispatcher(0x0043, 2, 2, 0x0017);
    fixture.ReturnStack.MoveStackPointer(-2);
    fixture.ReturnStack.WriteLocal(2, 0xA082, 2);
    Assert(fixture.Dispatch() == 0, "local string address dispatch failed");
    Assert(fixture.DataStack.PopUl() == 2, "local string size was not preserved");
    uint address = fixture.DataStack.PopUl();
    fixture.ReturnStack.MoveStackPointer(-1);
    fixture.DataStack.PushUl(address);
    fixture.DataStack.PushUl(0);
    fixture.Functions[0x008F]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 0x82,
        "local address moved with the return stack");
}

static void TestStringStorage()
{
    foreach (ushort opcode in new ushort[] { 0x0164, 0x0165, 0x0166, 0x0167 })
    {
        DispatchFixture fixture = CreateDispatcher();
        var data = new AccessPointer();
        data.SetupDataArray(32);
        SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
        uint source = fixture.McodePointer.AppendBytes(new byte[] { 0x82, 0xA0, (byte)'X' });
        fixture.DataStack.PushUl(source);
        fixture.DataStack.PushUl(3);
        ushort[] operands = opcode switch
        {
            0x0164 => new ushort[] { 2, 0 },
            0x0165 => new ushort[] { 0, 0, 2, 0 },
            0x0166 => new ushort[] { 0, 2 },
            _ => Array.Empty<ushort>()
        };
        for (int i = 0; i < operands.Length; i++)
            fixture.McodePointer.WriteBytes((uint)(0x70 + i * 2), BitConverter.GetBytes(operands[i]));
        if (opcode == 0x0166) fixture.ReturnStack.MoveStackPointer(-4);
        if (opcode == 0x0167)
        {
            fixture.DataStack.PushUl(0x80000000);
            fixture.DataStack.PushUl(2);
        }
        fixture.Functions[opcode]();
        byte[] stored = opcode == 0x0166
            ? fixture.ReturnStack.GetBytes(fixture.ReturnStack.GetCurrentStackPointer() * 4, 10)
            : data.GetBytes(0, 10);
        Assert(stored.SequenceEqual(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0x82, 0xA0 }),
            $"string storage 0x{opcode:X4} did not write the header and complete character");
        Assert(fixture.DataStack.GetCurrentStackPointer() == fixture.InitialDataStackPointer,
            "string storage consumed the wrong number of stack words");

        if (opcode != 0x0166)
        {
            data.ResetDataIndex(0);
            data.SetUl4Ms(3, 0);
            data.ResetDataIndex(8);
            data.SetUb((byte)'A');
            data.ResetDataIndex(9);
            data.SetUb((byte)'B');
            data.ResetDataIndex(10);
            data.SetUb((byte)'C');
            fixture.DataStack.PushUl(0x80000008);
            fixture.DataStack.PushUl(3);
            fixture.DataStack.PushUl(0x80000001);
            fixture.DataStack.PushUl(4);
            fixture.Functions[0x0167]();
            Assert(data.GetBytes(9, 3).SequenceEqual(new byte[] { 65, 66, 67 }),
                "overlapping string storage changed the source before copying it");
        }
    }
}

static void TestDefaultStringStorage()
{
    foreach (uint oldAddress in new uint[] { 0, 0x80000010 })
    {
        DispatchFixture fixture = CreateDispatcher();
        var data = new AccessPointer();
        data.SetupDataArray(48);
        data.SetUl4Ms(1, 2);
        data.ResetDataIndex(10);
        data.SetUb(65);
        SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
        fixture.DataStack.PushUl(oldAddress);
        fixture.DataStack.PushUl(oldAddress == 0 ? 0u : 7u);
        fixture.DataStack.PushUl(0x80000000);
        fixture.DataStack.PushUl(8);
        fixture.Functions[0x0171]();
        Assert(fixture.DataStack.PopUl() == (oldAddress == 0 ? 8u : 7u), "default capacity differs");
        Assert(fixture.DataStack.PopUl() == (oldAddress == 0 ? 0x80000000u : oldAddress),
            "default address overwrote an explicit address");
        fixture.DataStack.PushUl(0x80000000);
        fixture.DataStack.PushUl(8);
        fixture.Functions[0x016A]();
        Assert(fixture.DataStack.PopUl() == 1 && fixture.DataStack.PopUl() == 0x8000000A,
            "string reference did not include the left margin");
        fixture.DataStack.PushUl(0x80000000);
        fixture.DataStack.PushUl(8);
        fixture.Functions[0x0169]();
        Assert(data.GetBytes(0, 8).All(value => value == 0), "string clear left a nonempty header");
    }
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

static void TestStringReferenceStubs()
{
    DispatchFixture fixture = CreateDispatcher();
    var data = new AccessPointer();
    data.SetupDataArray(32);
    data.SetUl(3);
    data.ResetDataIndex(4);
    data.SetUl(2);
    data.ResetDataIndex(10);
    data.SetUl(0x005A5948);
    data.ResetDataIndex(0);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);

    fixture.Functions[0x004B]();
    Assert(fixture.DataStack.PopUl() == 3, "indirect string length differs");
    uint address = fixture.DataStack.PopUl();
    Assert(address == 0x8000000A, "indirect string address did not include its header and margin");

    fixture = CreateDispatcher(0x0059, 0, 0x0017);
    fixture.ReturnStack.MoveStackPointer(-4);
    fixture.ReturnStack.WriteLocal(0, 2, 4);
    fixture.ReturnStack.WriteLocal(4, 1, 4);
    fixture.ReturnStack.WriteLocal(9, 0x00434241, 4);
    Assert(fixture.Dispatch() == 0, "local string reference dispatch failed");
    Assert(fixture.DataStack.PopUl() == 2, "local string length differs");
    address = fixture.DataStack.PopUl();
    Assert((address & 0xC0000000) == 0xC0000000, "local string address lost its return-stack region");
    Assert(fixture.ReturnStack.GetBytes(address & 0x3FFFFFFF, 2).SequenceEqual(new byte[] { 0x41, 0x42 }),
        "local string address did not point past its header and margin");
}

static void TestMemoryCopyOpcodes()
{
    foreach (ushort opcode in new ushort[] { 0x0060, 0x0061 })
    {
        DispatchFixture fixture = CreateDispatcher();
        var data = new AccessPointer();
        data.SetupDataArray(32);
        data.SetUll(0x0807060504030201);
        SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);

        fixture.DataStack.PushUl(0x80000000);
        fixture.DataStack.PushUl(6);
        fixture.DataStack.PushUl(0x80000002);
        fixture.DataStack.PushUl(4);
        fixture.Functions[opcode]();
        Assert(data.GetBytes(0, 8).SequenceEqual(new byte[] { 1, 2, 1, 2, 3, 4, 7, 8 }),
            $"opcode 0x{opcode:X4} did not perform a truncated overlapping move");
        Assert(fixture.DataStack.IsCurrentStackPointerInit(), $"opcode 0x{opcode:X4} did not consume both string infos");
    }

    DispatchFixture resultFixture = CreateDispatcher();
    var resultData = new AccessPointer();
    resultData.SetupDataArray(32);
    resultData.SetUll(0x0000000000332211);
    SetField(typeof(Dispatcher), resultFixture.Dispatcher, "ap", resultData);
    resultFixture.DataStack.PushUl(0x80000000);
    resultFixture.DataStack.PushUl(3);
    resultFixture.DataStack.PushUl(0x80000010);
    resultFixture.DataStack.PushUl(6);
    resultFixture.Functions[0x0062]();
    Assert(resultData.GetBytes(16, 3).SequenceEqual(new byte[] { 0x11, 0x22, 0x33 }),
        "memory-copy-with-result did not copy the source bytes");
    Assert(resultFixture.DataStack.PopUl() == 0, "copy byte-count Q fraction differs");
    Assert(resultFixture.DataStack.PopUl() == 3, "copy byte-count differs");
    Assert(resultFixture.DataStack.PopUl() == 3, "remaining target length differs");
    Assert(resultFixture.DataStack.PopUl() == 0x80000013, "remaining target address differs");
}

static void TestIndirectMemoryAccess()
{
    foreach ((ushort opcode, uint expected) in new (ushort, uint)[]
        { (0x008F, 0xEF), (0x0090, 0xCDEF), (0x0091, 0x89ABCDEF) })
    {
        DispatchFixture fixture = CreateDispatcher();
        var data = new AccessPointer();
        data.SetupDataArray(16);
        data.SetUll(0x0123456789ABCDEF);
        SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
        fixture.DataStack.PushUl(0x80000000);
        fixture.DataStack.PushUl(0);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"opcode 0x{opcode:X4} loaded an incorrect value");
    }

    foreach ((ushort opcode, uint value, uint expected) in new (ushort, uint, uint)[]
        { (0x0092, 0x123456EF, 0xEF), (0x0093, 0x1234CDEF, 0xCDEF), (0x0094, 0x89ABCDEF, 0x89ABCDEF) })
    {
        DispatchFixture fixture = CreateDispatcher();
        var data = new AccessPointer();
        data.SetupDataArray(16);
        SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
        fixture.DataStack.PushUl(value);
        fixture.DataStack.PushUl(0);
        fixture.DataStack.PushUl(0x80000004);
        fixture.DataStack.PushUl(0);
        fixture.Functions[opcode]();
        Assert(data.GetBytes(4, 4).SequenceEqual(BitConverter.GetBytes(expected)),
            $"opcode 0x{opcode:X4} stored an incorrect value");
        Assert(fixture.DataStack.IsCurrentStackPointerInit(), $"opcode 0x{opcode:X4} did not consume both values");
    }
}

static void TestFloatingVariableOpcodes()
{
    DispatchFixture fixture = CreateDispatcher();
    var data = new AccessPointer();
    data.SetupDataArray(8);
    data.SetUll(unchecked((ulong)BitConverter.DoubleToInt64Bits(1.5)));
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);

    PushFloat(fixture.DataStack, 2.25);
    fixture.Functions[0x0089]();
    Assert(BitConverter.Int64BitsToDouble(unchecked((long)data.GetUll())) == 3.75,
        "floating variable addition differs");
    PushFloat(fixture.DataStack, 0.5);
    fixture.Functions[0x008E]();
    Assert(BitConverter.Int64BitsToDouble(unchecked((long)data.GetUll())) == 3.25,
        "floating variable subtraction differs");
    fixture.Functions[0x0073]();
    Assert(data.GetUll() == 0, "floating variable clear did not zero all 64 bits");

    fixture = CreateDispatcher();
    PushFloat(fixture.DataStack, 12.5);
    TextWriter original = Console.Out;
    using var output = new StringWriter(CultureInfo.InvariantCulture);
    try
    {
        Console.SetOut(output);
        fixture.Functions[0x0084]();
    }
    finally
    {
        Console.SetOut(original);
    }
    Assert(PopFloat(fixture.DataStack) == 12.5, "test float opcode did not restore its 64-bit value");
    Assert(output.ToString().Contains("12.50000", StringComparison.Ordinal),
        "test float opcode did not report the decimal value");

    fixture = CreateDispatcher();
    fixture.DataStack.PushUl(0x11223344);
    fixture.DataStack.PushUl(0x55667788);
    original = Console.Out;
    using var quadOutput = new StringWriter(CultureInfo.InvariantCulture);
    try
    {
        Console.SetOut(quadOutput);
        fixture.Functions[0x0083]();
    }
    finally
    {
        Console.SetOut(original);
    }
    Assert(fixture.DataStack.PopUl() == 0x55667788 && fixture.DataStack.PopUl() == 0x3344,
        "test quad opcode did not preserve the C helper's 64-bit stack layout");
    Assert(quadOutput.ToString().Contains("5566778811223344", StringComparison.Ordinal),
        "test quad opcode did not report the 64-bit value");
}

static void TestFloatingConstantsAndStackInfo()
{
    var constants = new (ushort Opcode, double Value)[]
    {
        (0x00A7, 0.0), (0x00A8, 1.0), (0x00A9, Math.E),
        (0x00AA, Math.Log2(Math.E)), (0x00AB, Math.Log10(Math.E)),
        (0x00AC, Math.Log(2.0)), (0x00AD, Math.Log(10.0)),
        (0x00AE, Math.PI), (0x00AF, Math.PI / 2.0),
        (0x00B0, Math.PI / 4.0), (0x00B1, 1.0 / Math.PI),
        (0x00B2, 2.0 / Math.PI), (0x00B3, 2.0 / Math.Sqrt(Math.PI)),
        (0x00B4, Math.Sqrt(2.0)), (0x00B5, 1.0 / Math.Sqrt(2.0))
    };

    foreach ((ushort opcode, double expected) in constants)
    {
        DispatchFixture fixture = CreateDispatcher();
        fixture.Functions[opcode]();
        double actual = PopFloat(fixture.DataStack);
        Assert(Math.Abs(actual - expected) <= Math.Abs(expected) * 1e-15 + 1e-15,
            $"opcode 0x{opcode:X4} pushed {actual} instead of {expected}");
        Assert(fixture.DataStack.IsCurrentStackPointerInit(),
            $"opcode 0x{opcode:X4} did not push one 64-bit value");
    }

    DispatchFixture pointers = CreateDispatcher();
    var dataArea = new AccessPointer();
    dataArea.SetupDataArray(32);
    SetField(typeof(Dispatcher), pointers.Dispatcher, "ap", dataArea);
    pointers.DataStack.PushUl(0x1234);
    pointers.ReturnStack.PushUl(0x5678);
    pointers.Functions[0x00B6]();
    uint dataBase = pointers.DataStack.PopUl();
    uint dataCurrent = pointers.DataStack.PopUl();
    Assert(dataCurrent == (0x40000000 | ((pointers.InitialDataStackPointer - 1) * 4))
        && dataBase == (0x40000000 | (pointers.InitialDataStackPointer * 4)),
        "data-stack pointer opcode returned incorrect current/base addresses");
    pointers.Functions[0x00B7]();
    uint returnBase = pointers.DataStack.PopUl();
    uint returnCurrent = pointers.DataStack.PopUl();
    Assert(returnCurrent == (0xC0000000 | ((pointers.InitialReturnStackPointer - 1) * 4))
        && returnBase == (0xC0000000 | (pointers.InitialReturnStackPointer * 4)),
        "return-stack pointer opcode returned incorrect current/base addresses");
    pointers.Functions[0x00B8]();
    Assert(pointers.DataStack.PopUl() == 32
        && pointers.DataStack.PopUl() == 0x80000000,
        "data-area opcode returned incorrect address/size");

    DispatchFixture parts = CreateDispatcher();
    parts.DataStack.PushUl(0x11111111);
    parts.DataStack.PushUl(0x22222222);
    parts.Functions[0x00B9]();
    Assert(parts.DataStack.GetUlOffset(1) == 0x11111111 && parts.DataStack.GetUlOffset(0) == 0,
        "fraction clear did not preserve the integer part");
    parts.DataStack.SetUlOffset(0x33333333, 0);
    parts.Functions[0x00BA]();
    Assert(parts.DataStack.GetUlOffset(1) == 0x33333333 && parts.DataStack.GetUlOffset(0) == 0,
        "fraction move did not transfer to the integer part");
    parts.DataStack.SetUlOffset(0x44444444, 0);
    parts.Functions[0x00BB]();
    Assert(parts.DataStack.GetUlOffset(1) == 0 && parts.DataStack.GetUlOffset(0) == 0x44444444,
        "integer clear did not preserve the fraction part");
}

static void TestFloatingMathOpcodes()
{
    var binaryOperations = new (ushort Opcode, double Left, double Right, double Expected)[]
    {
        (0x0144, 1.25, 2.5, 3.75),
        (0x0145, 1.25, 2.5, -1.25),
        (0x0146, 1.25, 2.5, 3.125),
        (0x0147, 1.25, 2.5, 0.5),
        (0x015A, 2, 3, 8)
    };

    foreach ((ushort opcode, double left, double right, double expected) in binaryOperations)
    {
        DispatchFixture fixture = CreateDispatcher();
        PushFloat(fixture.DataStack, left);
        PushFloat(fixture.DataStack, right);
        fixture.Functions[opcode]();
        Assert(Math.Abs(PopFloat(fixture.DataStack) - expected) < 1e-14,
            $"opcode 0x{opcode:X4} returned an incorrect result");
        Assert(fixture.DataStack.IsCurrentStackPointerInit(),
            $"opcode 0x{opcode:X4} did not consume both operands");
    }

    var comparisons = new (ushort Opcode, double Left, double Right, bool Expected)[]
    {
        (0x0148, 2.5, 2.5, true),
        (0x0149, 2.5, 1.25, true),
        (0x014A, 1.25, 2.5, true),
        (0x014B, 2.5, 2.5, true),
        (0x014C, 1.25, 2.5, true),
        (0x014D, 1.25, 2.5, true)
    };

    foreach ((ushort opcode, double left, double right, bool expected) in comparisons)
    {
        DispatchFixture fixture = CreateDispatcher();
        PushFloat(fixture.DataStack, left);
        PushFloat(fixture.DataStack, right);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0
            && fixture.DataStack.PopUl() == (expected ? 1u : 0u),
            $"opcode 0x{opcode:X4} returned an incorrectly laid out comparison result");
        Assert(fixture.DataStack.IsCurrentStackPointerInit(),
            $"opcode 0x{opcode:X4} did not consume both operands");
    }

    var unaryOperations = new (ushort Opcode, double Input, double Expected)[]
    {
        (0x014E, 0.5, Math.Acos(0.5)),
        (0x014F, 0.5, Math.Asin(0.5)),
        (0x0150, 0.5, Math.Atan(0.5)),
        (0x0151, 0.5, Math.Cos(0.5)),
        (0x0152, 0.5, Math.Sin(0.5)),
        (0x0153, 0.5, Math.Tan(0.5)),
        (0x0154, 0.5, Math.Cosh(0.5)),
        (0x0155, 0.5, Math.Sinh(0.5)),
        (0x0156, 0.5, Math.Tanh(0.5)),
        (0x0157, 0.5, Math.Exp(0.5)),
        (0x0158, 2, Math.Log(2)),
        (0x0159, 2, Math.Log10(2)),
        (0x015B, 4, 2),
        (0x015C, -0.5, 0.5),
        (0x015D, 180, Math.PI),
        (0x015E, Math.PI, 180)
    };

    foreach ((ushort opcode, double input, double expected) in unaryOperations)
    {
        DispatchFixture fixture = CreateDispatcher();
        PushFloat(fixture.DataStack, input);
        fixture.Functions[opcode]();
        Assert(Math.Abs(PopFloat(fixture.DataStack) - expected) < 1e-14,
            $"opcode 0x{opcode:X4} returned an incorrect result");
        Assert(fixture.DataStack.IsCurrentStackPointerInit(),
            $"opcode 0x{opcode:X4} did not consume one operand");
    }
}

static void TestIntegerUtilityOpcodes()
{
    foreach ((ushort opcode, int left, int right, uint expected) in new (ushort, int, int, uint)[]
        { (0x010B, 7, 3, 21), (0x010F, 17, 5, 2), (0x0116, -4, 6, unchecked((uint)-4)),
          (0x0117, -4, 6, 6) })
    {
        DispatchFixture fixture = CreateDispatcher();
        PushIntegerQuad(fixture.DataStack, unchecked((uint)left));
        PushIntegerQuad(fixture.DataStack, unchecked((uint)right));
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"opcode 0x{opcode:X4} returned an incorrect result");
    }

    foreach ((ushort opcode, int left, int right, bool expected) in new (ushort, int, int, bool)[]
        { (0x0113, -2, 1, true), (0x0115, 2, 1, true) })
    {
        DispatchFixture fixture = CreateDispatcher();
        PushIntegerQuad(fixture.DataStack, unchecked((uint)left));
        PushIntegerQuad(fixture.DataStack, unchecked((uint)right));
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0
            && fixture.DataStack.PopUl() == (expected ? 1u : 0u),
            $"opcode 0x{opcode:X4} returned an incorrectly laid out comparison result");
    }

    DispatchFixture quotientAndRemainder = CreateDispatcher();
    PushIntegerQuad(quotientAndRemainder.DataStack, 17);
    PushIntegerQuad(quotientAndRemainder.DataStack, 5);
    quotientAndRemainder.Functions[0x010E]();
    Assert(quotientAndRemainder.DataStack.PopUl() == 0
        && quotientAndRemainder.DataStack.PopUl() == 2
        && quotientAndRemainder.DataStack.PopUl() == 0
        && quotientAndRemainder.DataStack.PopUl() == 3,
        "syouToAmari returned quotient and remainder in the wrong order");

    foreach ((ushort opcode, int value, bool expected) in new (ushort, int, bool)[]
        { (0x011F, -1, false), (0x011F, 0, true), (0x0120, 0, false), (0x0120, 1, true) })
    {
        DispatchFixture fixture = CreateDispatcher();
        PushIntegerQuad(fixture.DataStack, unchecked((uint)value));
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.GetUlOffset(1) == (expected ? 1u : 0u)
            && fixture.DataStack.GetUlOffset(0) == 0,
            $"opcode 0x{opcode:X4} did not update only the integer word");
    }

    DispatchFixture range = CreateDispatcher();
    PushIntegerQuad(range.DataStack, 5);
    PushIntegerQuad(range.DataStack, 3);
    PushIntegerQuad(range.DataStack, 6);
    range.Functions[0x0118]();
    Assert(range.DataStack.PopUl() == 0 && range.DataStack.PopUl() == 1,
        "isHanni did not include both bounds");

    DispatchFixture either = CreateDispatcher();
    PushIntegerQuad(either.DataStack, 5);
    PushIntegerQuad(either.DataStack, 4);
    PushIntegerQuad(either.DataStack, 5);
    either.Functions[0x0119]();
    Assert(either.DataStack.PopUl() == 0 && either.DataStack.PopUl() == 1,
        "dotirakaniHitosii did not match the second value");

    DispatchFixture absolute = CreateDispatcher();
    PushIntegerQuad(absolute.DataStack, unchecked((uint)-7));
    absolute.Functions[0x011B]();
    Assert(absolute.DataStack.PopUl() == 0 && absolute.DataStack.PopUl() == 7,
        "zettaiti did not return the absolute value");

    DispatchFixture pair = CreateDispatcher();
    pair.DataStack.PushUl(0x11111111);
    pair.DataStack.PushUl(0xAAAA);
    pair.DataStack.PushUl(0x22222222);
    pair.DataStack.PushUl(0xBBBB);
    pair.Functions[0x011D]();
    Assert(pair.DataStack.PopUl() == 0xBBBB && pair.DataStack.PopUl() == 0x22222222
        && pair.DataStack.PopUl() == 0xAAAA && pair.DataStack.PopUl() == 0x11111111
        && pair.DataStack.PopUl() == 0xBBBB && pair.DataStack.PopUl() == 0x22222222
        && pair.DataStack.PopUl() == 0xAAAA && pair.DataStack.PopUl() == 0x11111111,
        "pairDeFukusya did not append the same two values");

    foreach ((ushort opcode, uint input, uint expected) in new (ushort, uint, uint)[]
        {
            (0x0125, 0x123456AB, 0x123456BA),
            (0x0126, 0x1234ABCD, 0x1234CDAB),
            (0x0127, 0x1234ABCD, 0x1234CDAB),
            (0x0128, 0x1234ABCD, 0xABCD1234),
            (0x0129, 0x12345678, 0x78563412),
            (0x012C, 0x1234ABCD, 0xC),
            (0x012D, 0x1234ABCD, 0xD),
            (0x012E, 0x1234ABCD, 0xAB),
            (0x012F, 0x1234ABCD, 0xCD),
            (0x0130, 0x1234ABCD, 0x1234),
            (0x0131, 0x1234ABCD, 0xABCD),
            (0x0132, 0x10000003, 0x30),
            (0x0133, 0x80000000, 0x08000000),
            (0x0134, 0xF0F0, 0xA0A0),
            (0x0135, 0xF000, 0xF5A5),
            (0x0136, 0xF0F0, 0x5A5A),
            (0x0137, 0x12345678, 0xEDCBA987)
        })
    {
        DispatchFixture fixture = CreateDispatcher();
        PushIntegerQuad(fixture.DataStack, input);
        if (opcode is 0x0132 or 0x0133)
            PushIntegerQuad(fixture.DataStack, 4);
        else if (opcode is 0x0134 or 0x0135 or 0x0136)
            PushIntegerQuad(fixture.DataStack, opcode == 0x0134 ? 0xA0A0u : opcode == 0x0135 ? 0x05A5u : 0xAAAAu);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"opcode 0x{opcode:X4} returned an incorrect result");
    }

    foreach ((ushort opcode, uint value, uint immediate, uint expected, bool wide) in new (ushort, uint, uint, uint, bool)[]
        {
            (0x0139, 0xF0F0F0F0, 0x0FF00FF0, 0x00F000F0, true),
            (0x013A, 0x12340000, 0xABCD, 0x1234ABCD, false),
            (0x013B, 0x12340000, 0xABCD, 0x1234ABCD, true)
        })
    {
        DispatchFixture fixture = CreateDispatcher();
        if (wide)
            WriteMcodeUInts(fixture, 0x70, immediate);
        else
            WriteMcodeUShorts(fixture, 0x70, checked((ushort)immediate));
        PushIntegerQuad(fixture.DataStack, value);
        fixture.Functions[opcode]();
        Assert(fixture.McodePointer.GetMcodeIndex() == (wide ? 0x74u : 0x72u)
            && fixture.DataStack.PopUl() == 0
            && fixture.DataStack.PopUl() == expected,
            $"opcode 0x{opcode:X4} read an incorrect immediate or result");
    }

    DispatchFixture packed = CreateDispatcher();
    PushIntegerQuad(packed.DataStack, 0x1234);
    PushIntegerQuad(packed.DataStack, 0xABCD);
    packed.Functions[0x013C]();
    Assert(packed.DataStack.PopUl() == 0 && packed.DataStack.PopUl() == 0x1234ABCD,
        "pack did not combine the high and low words");

    DispatchFixture unpacked = CreateDispatcher();
    PushIntegerQuad(unpacked.DataStack, 0x1234ABCD);
    unpacked.Functions[0x013D]();
    Assert(unpacked.DataStack.PopUl() == 0 && unpacked.DataStack.PopUl() == 0xABCD
        && unpacked.DataStack.PopUl() == 0 && unpacked.DataStack.PopUl() == 0x1234,
        "unpack did not return the high and low words");

    DispatchFixture memory = CreateDispatcher();
    AccessPointer data = AttachDataPointer(memory, 32);
    for (uint index = 0; index < 3; index++)
    {
        data.ResetDataIndex(index);
        data.SetUb((byte)(index + 1));
    }
    PushIntegerQuad(memory.DataStack, 0x5A);
    PushIntegerQuad(memory.DataStack, 0x80000008);
    PushIntegerQuad(memory.DataStack, 4);
    memory.Functions[0x013E]();
    Assert(data.GetBytes(8, 4).SequenceEqual(new byte[] { 0x5A, 0x5A, 0x5A, 0x5A }),
        "mitasu did not fill the requested memory range");

    foreach (ushort opcode in new ushort[] { 0x013F, 0x0140 })
    {
        PushIntegerQuad(memory.DataStack, 0x80000000);
        PushIntegerQuad(memory.DataStack, 0x80000010);
        PushIntegerQuad(memory.DataStack, 3);
        memory.Functions[opcode]();
        Assert(data.GetBytes(16, 3).SequenceEqual(new byte[] { 1, 2, 3 }),
            $"tenso alias 0x{opcode:X4} did not copy the requested bytes");
    }

    foreach ((ushort opcode, uint expected) in new (ushort, uint)[]
        { (0x012A, BitConverter.IsLittleEndian ? 0u : 1u),
          (0x012B, BitConverter.IsLittleEndian ? 1u : 0u) })
    {
        DispatchFixture fixture = CreateDispatcher();
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"opcode 0x{opcode:X4} reported the wrong host byte order");
    }

    DispatchFixture fraction = CreateDispatcher();
    PushIntegerQuad(fraction.DataStack, 5);
    PushIntegerQuad(fraction.DataStack, 2);
    fraction.Functions[0x0141]();
    Assert(Math.Abs(PopFloat(fraction.DataStack) - 2.5) < 1e-14,
        "syousuumadewaru did not convert integer division to double");

    DispatchFixture toFloat = CreateDispatcher();
    PushIntegerQuad(toFloat.DataStack, unchecked((uint)-6));
    toFloat.Functions[0x0142]();
    Assert(PopFloat(toFloat.DataStack) == -6, "syousuka lost the integer sign");

    DispatchFixture toInteger = CreateDispatcher();
    PushFloat(toInteger.DataStack, 7.9);
    toInteger.Functions[0x0143]();
    Assert(toInteger.DataStack.PopUl() == 0 && toInteger.DataStack.PopUl() == 7,
        "seisuka did not truncate the double to an integer");
}

static void PushFloat(StackPointer stack, double value)
{
    ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
    stack.PushUl((uint)(bits >> 32));
    stack.PushUl((uint)bits);
}

static void TestConsoleNumericDisplay()
{
    DispatchFixture fixture = CreateDispatcher();
    var data = new AccessPointer();
    data.SetupDataArray(16);
    data.SetUll(0x6948);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);

    using var output = new StringWriter(CultureInfo.InvariantCulture);
    TextWriter original = Console.Out;
    try
    {
        Console.SetOut(output);
        fixture.DataStack.PushUl(0x80000000);
        fixture.DataStack.PushUl(2);
        fixture.Functions[0x00C2]();

        PushIntegerQuad(fixture.DataStack, 0x41);
        fixture.Functions[0x00C3]();
        PushIntegerQuad(fixture.DataStack, 0x82A0);
        fixture.Functions[0x00C3]();
        PushIntegerQuad(fixture.DataStack, 0x1F);
        fixture.Functions[0x00C5]();
        PushIntegerQuad(fixture.DataStack, 0xA7);
        fixture.Functions[0x00C6]();
        PushIntegerQuad(fixture.DataStack, 0xFA12);
        fixture.Functions[0x00C7]();
        PushIntegerQuad(fixture.DataStack, 0xABCDEF12);
        fixture.Functions[0x00C8]();
        PushFloat(fixture.DataStack, 1.25);
        PushIntegerQuad(fixture.DataStack, 2);
        fixture.Functions[0x00CB]();
        PushFloat(fixture.DataStack, 1.25);
        PushIntegerQuad(fixture.DataStack, 2);
        fixture.Functions[0x00CC]();
        fixture.DataStack.PushUl(0x1234);
        fixture.DataStack.PushUl(0x56);
        fixture.Functions[0x00CE]();
    }
    finally
    {
        Console.SetOut(original);
    }
    string actualOutput = output.ToString();
    Assert(actualOutput == "\"Hi\"AあFA7FA12ABCDEF121.251.25e+00000001234|00000056",
        $"numeric/string display opcodes did not match their expected format: [{actualOutput.Replace("\r", "\\r").Replace("\n", "\\n")}]");
    Assert(fixture.DataStack.IsCurrentStackPointerInit(),
        "display opcodes did not consume their stack arguments");

    fixture = CreateDispatcher();
    data = new AccessPointer();
    data.SetupDataArray(8);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);
    PushIntegerQuad(fixture.DataStack, 0x41);
    PushIntegerQuad(fixture.DataStack, 0x80000000);
    fixture.Functions[0x00C0]();
    Assert(data.GetBytes(0, 2).SequenceEqual(new byte[] { 0x41, 0x11 }),
        "wordStore11 did not add the debug high byte for an 8-bit value");
}

static void TestAtofAndFractionPacking()
{
    DispatchFixture fixture = CreateDispatcher();
    var data = new AccessPointer();
    data.SetupDataArray(64);
    byte[] number = System.Text.Encoding.ASCII.GetBytes("  -12.5e2rest");
    for (uint i = 0; i < number.Length; i++)
    {
        data.ResetDataIndex(i);
        data.SetUb(number[i]);
    }
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);

    fixture.DataStack.PushUl(0x80000000);
    fixture.DataStack.PushUl((uint)number.Length);
    PushIntegerQuad(fixture.DataStack, 0x80000020);
    fixture.Functions[0x00A6]();
    Assert(PopFloat(fixture.DataStack) == -1250.0, "c_atof parsed an incorrect numeric prefix");
    Assert(BitConverter.ToUInt32(data.GetBytes(32, 4)) == 0x80000009,
        "c_atof did not store the end pointer after the parsed prefix");
    Assert(fixture.DataStack.IsCurrentStackPointerInit(),
        "c_atof did not consume its arguments and return one double");

    fixture = CreateDispatcher();
    fixture.DataStack.PushUl(0x12345678);
    fixture.DataStack.PushUl(0x9ABCDEF0);
    fixture.Functions[0x00BC]();
    Assert(fixture.DataStack.PopUl() == 0x9ABCDEF0 && fixture.DataStack.PopUl() == 0x12345678,
        "integer-to-fraction pack did not preserve the upper/lower words");

    fixture = CreateDispatcher();
    fixture.DataStack.PushUl(0x12345678);
    fixture.DataStack.PushUl(0x9ABCDEF0);
    fixture.Functions[0x00BD]();
    Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 0x9ABCDEF0
        && fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == 0x12345678,
        "integer-to-fraction unpack did not produce two zero-fraction values");
}

static void TestConsoleMemoryDisplays()
{
    DispatchFixture fixture = CreateDispatcher();
    var data = new AccessPointer();
    data.SetupDataArray(32);
    data.SetUll(0x0807060504030201);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);

    TextWriter original = Console.Out;
    using var output = new StringWriter(CultureInfo.InvariantCulture);
    try
    {
        Console.SetOut(output);
        fixture.DataStack.PushUl(0x80000000);
        fixture.DataStack.PushUl(3);
        fixture.Functions[0x00CF]();
        fixture.DataStack.PushUl(0x80000000);
        fixture.DataStack.PushUl(3);
        fixture.Functions[0x00D0]();
        fixture.Functions[0x00D2]();
    }
    finally
    {
        Console.SetOut(original);
    }

    string lineEnd = Environment.NewLine;
    string expected = $"80000000 : 01 02 03{lineEnd}80000000 : 0201 0403{lineEnd}"
        + $"ReturnStackPointer={0xC0000000u | (fixture.InitialReturnStackPointer * 4):X8} "
        + $"(base={0xC0000000u | (fixture.InitialReturnStackPointer * 4):X8}){lineEnd}";
    Assert(output.ToString() == expected, "memory/return-stack display output differed");
    Assert(fixture.DataStack.IsCurrentStackPointerInit(),
        "memory display opcodes did not consume their stack arguments");
}

static void TestNumericStringConversions()
{
    DispatchFixture fixture = CreateDispatcher();
    var data = new AccessPointer();
    data.SetupDataArray(128);
    SetField(typeof(Dispatcher), fixture.Dispatcher, "ap", data);

    long integer = long.MinValue;
    fixture.DataStack.PushUl(unchecked((uint)((ulong)integer >> 32)));
    fixture.DataStack.PushUl(unchecked((uint)integer));
    fixture.DataStack.PushUl(0x80000000);
    fixture.DataStack.PushUl(32);
    fixture.Functions[0x00CA]();

    uint integerLength = fixture.DataStack.PopUl();
    uint integerAddress = fixture.DataStack.PopUl();
    Assert(integerAddress == 0x80000008 && integerLength == 20,
        "quad conversion returned an incorrect string reference");
    Assert(System.Text.Encoding.ASCII.GetString(data.GetBytes(8, integerLength)) == "-9223372036854775808"
        && System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data.GetBytes(0, 4)) == integerLength,
        "quad conversion did not store the signed decimal value and length");

    PushDouble(fixture.DataStack, 12.5);
    PushIntegerQuad(fixture.DataStack, 8);
    PushIntegerQuad(fixture.DataStack, 2);
    fixture.DataStack.PushUl(0x80000020);
    fixture.DataStack.PushUl(16);
    PushIntegerQuad(fixture.DataStack, 0);
    fixture.Functions[0x00CD]();

    uint fixedLength = fixture.DataStack.PopUl();
    uint fixedAddress = fixture.DataStack.PopUl();
    Assert(fixedAddress == 0x80000028 && fixedLength == 8
        && System.Text.Encoding.ASCII.GetString(data.GetBytes(40, fixedLength)) == "   12.50",
        "fraction conversion did not apply fixed precision and field width");

    PushDouble(fixture.DataStack, 12.5);
    PushIntegerQuad(fixture.DataStack, 0);
    PushIntegerQuad(fixture.DataStack, 2);
    fixture.DataStack.PushUl(0x80000040);
    fixture.DataStack.PushUl(6);
    PushIntegerQuad(fixture.DataStack, 1);
    fixture.Functions[0x00CD]();

    uint truncatedLength = fixture.DataStack.PopUl();
    uint truncatedAddress = fixture.DataStack.PopUl();
    Assert(truncatedAddress == 0x80000048 && truncatedLength == 5
        && System.Text.Encoding.ASCII.GetString(data.GetBytes(72, truncatedLength)) == "1.25e",
        "fraction conversion did not truncate to the buffer size");
}

static void TestDataStackDump()
{
    DispatchFixture fixture = CreateDispatcher(0x00D1, 0x0017);
    fixture.DataStack.PushUl(0x11223344);
    fixture.DataStack.PushUl(0xAABBCCDD);

    TextWriter original = Console.Out;
    using var output = new StringWriter(CultureInfo.InvariantCulture);
    try
    {
        Console.SetOut(output);
        Assert(fixture.Dispatch() == 0, "data-stack dump dispatch failed");
    }
    finally
    {
        Console.SetOut(original);
    }

    string newline = Environment.NewLine;
    string expected = $"-------------------------------------------------------{newline}"
        + $"sp=40000038 base=40000040{newline}"
        + $"スタック内容： 11223344|AABBCCDD  {newline}"
        + $"-------------------------------------------------------{newline}";
    Assert(output.ToString() == expected, "data-stack dump output differed from the C format");
    Assert(fixture.DataStack.IsCurrentStackPointerInit(),
        "data-stack dump did not reset the data stack");

    fixture = CreateDispatcher(0x00D1, 0x0017);
    fixture.DataStack.PushUl(0x12345678);
    output.GetStringBuilder().Clear();
    try
    {
        Console.SetOut(output);
        Assert(fixture.Dispatch() == 0, "invalid data-stack dump dispatch failed");
    }
    finally
    {
        Console.SetOut(original);
    }
    Assert(output.ToString().Contains("スタックポインタが異常です．", StringComparison.Ordinal)
        && fixture.DataStack.IsCurrentStackPointerInit(),
        "data-stack dump did not detect and reset a non-quad-aligned stack");
}

static void TestReturnCountAndCaseOpcodes()
{
    foreach ((ushort opcode, uint expected) in new (ushort, uint)[]
        { (0x00F2, 0), (0x00F3, 2), (0x00F4, 3), (0x00F5, 2),
          (0x00F6, 1), (0x00F7, 3), (0x00F8, 5) })
    {
        DispatchFixture fixture = CreateDispatcher();
        fixture.ReturnStack.PushUl(5);
        fixture.ReturnStack.PushUl(2);
        fixture.ReturnStack.PushUl(3);
        fixture.ReturnStack.PushUl(1);
        fixture.Functions[opcode]();
        Assert(fixture.DataStack.PopUl() == 0 && fixture.DataStack.PopUl() == expected,
            $"return-count opcode 0x{opcode:X4} read the wrong loop level");
    }

    foreach ((uint value, uint expected) in new (uint, uint)[] { (0, 0), (0xFFFFFFFF, 1) })
    {
        DispatchFixture fixture = CreateDispatcher();
        fixture.DataStack.PushUl(value);
        fixture.DataStack.PushUl(0x12345678);
        fixture.Functions[0x00FF]();
        Assert(fixture.DataStack.GetUlOffset(1) == expected
            && fixture.DataStack.GetUlOffset(0) == 0x12345678,
            "isTrue did not update only the integer word");
    }

    DispatchFixture select = CreateDispatcher();
    WriteRelativeDispatchTable(select, 0x80, 0x90, 0xA0, 0xB0);
    PushIntegerQuad(select.DataStack, 2);
    select.Functions[0x00F9]();
    Assert(select.McodePointer.GetMcodeIndex() == 0xA0,
        "select did not branch to the one-based table entry");

    DispatchFixture numericCase = CreateDispatcher();
    WriteRelativeDispatchTable(numericCase, 0x80, 0x90, 0xA0, 0xB0);
    WriteMcodeUInts(numericCase, 0x90, 10);
    WriteMcodeUInts(numericCase, 0xA0, 42);
    PushIntegerQuad(numericCase.DataStack, 42);
    numericCase.Functions[0x00FA]();
    Assert(numericCase.McodePointer.GetMcodeIndex() == 0xA4,
        "double case did not select a matching value");

    DispatchFixture numericRange = CreateDispatcher();
    WriteRelativeDispatchTable(numericRange, 0x80, 0x90, 0xA0, 0xB0);
    WriteMcodeUInts(numericRange, 0x90, 20);
    WriteMcodeUInts(numericRange, 0xA0, 40);
    PushIntegerQuad(numericRange.DataStack, 30);
    numericRange.Functions[0x00FB]();
    Assert(numericRange.McodePointer.GetMcodeIndex() == 0xA4,
        "double range did not select the first inclusive upper bound");

    DispatchFixture stringCase = CreateDispatcher();
    WriteRelativeDispatchTable(stringCase, 0x80, 0x90, 0xA0);
    WriteMcodeUShorts(stringCase, 0x90, 3);
    stringCase.McodePointer.WriteBytes(0x92, [(byte)'c', (byte)'a', (byte)'t', 0]);
    var data = AttachDataPointer(stringCase, 16);
    for (uint i = 0; i < 3; i++)
    {
        data.ResetDataIndex(i);
        data.SetUb((byte)"cat"[(int)i]);
    }
    stringCase.DataStack.PushUl(0x80000000);
    stringCase.DataStack.PushUl(3);
    stringCase.Functions[0x00FC]();
    Assert(stringCase.McodePointer.GetMcodeIndex() == 0x96,
        "string case did not skip the matching literal");

    DispatchFixture defaultCase = CreateDispatcher();
    WriteRelativeDispatchTable(defaultCase, 0x80, 0x90, 0xA0);
    WriteMcodeUShorts(defaultCase, 0x90, 3);
    defaultCase.McodePointer.WriteBytes(0x92, [(byte)'c', (byte)'a', (byte)'t', 0]);
    data = AttachDataPointer(defaultCase, 16);
    for (uint i = 0; i < 3; i++)
    {
        data.ResetDataIndex(i);
        data.SetUb((byte)"dog"[(int)i]);
    }
    defaultCase.DataStack.PushUl(0x80000000);
    defaultCase.DataStack.PushUl(3);
    defaultCase.Functions[0x00FC]();
    Assert(defaultCase.McodePointer.GetMcodeIndex() == 0xA0,
        "string case did not select the default table entry");
}

static void WriteRelativeDispatchTable(DispatchFixture fixture, uint table, params uint[] branches)
{
    short tableOffset = checked((short)((long)table - 0x70));
    WriteMcodeUShorts(fixture, 0x70, unchecked((ushort)tableOffset));
    ushort[] words = new ushort[branches.Length + 1];
    words[0] = checked((ushort)branches.Length);
    uint offsets = table + 2;
    for (int i = 0; i < branches.Length; i++)
        words[i + 1] = unchecked((ushort)checked((short)((long)branches[i] - offsets)));
    WriteMcodeUShorts(fixture, table, words);
}

static void PushIntegerQuad(StackPointer stack, uint value)
{
    stack.PushUl(value);
    stack.PushUl(0);
}

static void PushDouble(StackPointer stack, double value)
{
    ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
    stack.PushUl((uint)(bits >> 32));
    stack.PushUl((uint)bits);
}

static double PopFloat(StackPointer stack)
{
    uint low = stack.PopUl();
    uint high = stack.PopUl();
    return BitConverter.Int64BitsToDouble(unchecked((long)(((ulong)high << 32) | low)));
}

static void TestFixtureOutput(string name, bool nativeNewlines = false)
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
    Assert(error == "", $"{name} produced stderr without -D: {error}");
    byte[] expected = File.ReadAllBytes(referencePath);
    if (nativeNewlines)
    {
        using var native = new MemoryStream();
        foreach (byte value in expected)
        {
            if (value == 10) native.WriteByte(13);
            native.WriteByte(value);
        }
        expected = native.ToArray();
    }
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