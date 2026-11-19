namespace Mind8Kernel;

public partial class Dispatcher
{
    private (uint Address, uint Capacity) ReadJinfo()
    {
        ulong info = ap.GetUll();
        return ((uint)(info >> 32), (uint)info);
    }

    private uint RightMargin(uint address, uint capacity) =>
        checked(capacity - ReadMemoryWord(address) - ReadMemoryWord(AddAddress(address, 4)));

    private void TuikaYoyu()
    {
        uint remaining = RightMargin(AccessAddress, mp.FetchMcodeInt());
        dtp.PushUl(remaining);
        dtp.PushUl(0);
    }

    private void ItiMojiTuika()
    {
        uint address = AccessAddress;
        uint capacity = mp.FetchMcodeInt();
        dtp.PopUl();
        ushort character = (ushort)dtp.PopUl();
        byte[] bytes = character > 255
            ? [(byte)(character >> 8), (byte)character]
            : [(byte)character];
        uint length = ReadMemoryWord(address);
        uint margin = ReadMemoryWord(AddAddress(address, 4));
        if (RightMargin(address, capacity) < bytes.Length) return;
        WriteMemory(AddAddress(address, checked(8 + margin + length)), bytes);
        WriteMemoryWord(address, checked(length + (uint)bytes.Length));
    }

    private void ItiMojiHigaTuikaForJinfo()
    {
        (uint address, _) = ReadJinfo();
        dtp.PopUl();
        ushort character = (ushort)dtp.PopUl();
        byte[] bytes = character > 255
            ? [(byte)(character >> 8), (byte)character]
            : [(byte)character];
        uint margin = ReadMemoryWord(AddAddress(address, 4));
        if (margin < bytes.Length) return;
        margin -= (uint)bytes.Length;
        WriteMemory(AddAddress(address, checked(8 + margin)), bytes);
        WriteMemoryWord(address, checked(ReadMemoryWord(address) + (uint)bytes.Length));
        WriteMemoryWord(AddAddress(address, 4), margin);
    }

    private void Tuika()
    {
        uint address = AccessAddress;
        uint capacity = mp.FetchMcodeInt();
        uint length = dtp.PopUl();
        uint source = dtp.PopUl();
        byte[] content = ReadStringBytes(source, length);
        int count = FitStringLength(content, RightMargin(address, capacity));
        uint current = ReadMemoryWord(address);
        uint margin = ReadMemoryWord(AddAddress(address, 4));
        WriteMemory(AddAddress(address, checked(8 + margin + current)), content.AsSpan(0, count));
        WriteMemoryWord(address, checked(current + (uint)count));
    }

    private void MigiYoseForJinfo()
    {
        (uint address, uint capacity) = ReadJinfo();
        uint length = ReadMemoryWord(address);
        uint margin = ReadMemoryWord(AddAddress(address, 4));
        uint shift = RightMargin(address, capacity);
        byte[] content = ReadStringBytes(AddAddress(address, checked(8 + margin)), length);
        WriteMemory(AddAddress(address, checked(8 + margin + shift)), content);
        WriteMemoryWord(AddAddress(address, 4), checked(margin + shift));
    }
}
