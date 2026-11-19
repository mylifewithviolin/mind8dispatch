using System.Buffers.Binary;

namespace Mind8Kernel;

public partial class Dispatcher
{
    private const uint AddressMask = 0x3FFFFFFF;
    private const uint RegionMask = ~AddressMask;
    private const uint DataStackAddressBit = 0x40000000;
    private const uint ReturnStackAddressBit = 0xC0000000;

    private static uint AddAddress(uint address, uint offset)
    {
        uint position = checked((address & AddressMask) + offset);
        if (position > AddressMask) throw new ArgumentOutOfRangeException(nameof(offset), "Address crosses a memory region.");
        return (address & RegionMask) | position;
    }

    private byte[] ReadStringBytes(uint address, uint length)
    {
        AddAddress(address, length);
        uint position = address & AddressMask;
        return (address & RegionMask) switch
        {
            DataAddressBit => ap.GetBytes(position, length),
            DataStackAddressBit => dtp.GetBytes(position, length),
            ReturnStackAddressBit => rtp.GetBytes(position, length),
            _ => mp.GetUb(position, length)
        };
    }

    private void WriteMemory(uint address, ReadOnlySpan<byte> bytes)
    {
        AddAddress(address, checked((uint)bytes.Length));
        uint position = address & AddressMask;
        switch (address & RegionMask)
        {
            case DataAddressBit: ap.WriteDataBytes(position, bytes); break;
            case DataStackAddressBit: dtp.WriteBytes(position, bytes); break;
            case ReturnStackAddressBit: rtp.WriteBytes(position, bytes); break;
            default: mp.WriteBytes(position, bytes); break;
        }
    }

    private uint ReadMemoryWord(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadStringBytes(address, 4));

    private void WriteMemoryWord(uint address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        WriteMemory(address, bytes);
    }

    private uint AccessAddress =>
        (ap.Stack == dtp ? DataStackAddressBit : ap.Stack == rtp ? ReturnStackAddressBit
            : ap.Code is not null ? 0u : DataAddressBit) | ap.Address;

    private void PointAddress(uint address)
    {
        uint position = address & AddressMask;
        switch (address & RegionMask)
        {
            case DataAddressBit: ap.ResetDataIndex(position); break;
            case DataStackAddressBit: ap.PointStackAddress(dtp, position); break;
            case ReturnStackAddressBit: ap.PointStackAddress(rtp, position); break;
            default: ap.PointCode(mp, position); break;
        }
    }

    private uint LocalAddress(uint offset) =>
        AddAddress(ReturnStackAddressBit | checked(rtp.GetCurrentStackPointer() * 4), offset);
}
