using System.Buffers.Binary;
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