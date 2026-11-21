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

    private void TuikaYoyuForJinfo()
    {
        (uint address, uint capacity) = ReadJinfo();
        dtp.PushUl(RightMargin(address, capacity));
        dtp.PushUl(0);
    }

    private void HidariTuikaYoyu()
    {
        mp.FetchMcodeInt();
        dtp.PushUl(ReadMemoryWord(AddAddress(AccessAddress, 4)));
        dtp.PushUl(0);
    }

    private void HidariTuikaYoyuForJinfo()
    {
        (uint address, _) = ReadJinfo();
        dtp.PushUl(ReadMemoryWord(AddAddress(address, 4)));
        dtp.PushUl(0);
    }

    private void ItiMojiTuika()
    {
        uint address = AccessAddress;
        uint capacity = mp.FetchMcodeInt();
        AppendCharacterRight(address, capacity);
    }

    private void ItiMojiTuikaForJinfo()
    {
        (uint address, uint capacity) = ReadJinfo();
        AppendCharacterRight(address, capacity);
    }

    private void AppendCharacterRight(uint address, uint capacity)
    {
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

    private void ItiMojiHigaTuika()
    {
        mp.FetchMcodeInt();
        AppendCharacterLeft(AccessAddress);
    }

    private void ItiMojiHigaTuikaForJinfo()
    {
        (uint address, _) = ReadJinfo();
        AppendCharacterLeft(address);
    }

    private void AppendCharacterLeft(uint address)
    {
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
        AppendStringRight(address, capacity);
    }

    private void TuikaForJinfo()
    {
        (uint address, uint capacity) = ReadJinfo();
        AppendStringRight(address, capacity);
    }

    private void AppendStringRight(uint address, uint capacity)
    {
        uint length = dtp.PopUl();
        uint source = dtp.PopUl();
        byte[] content = ReadStringBytes(source, length);
        int count = FitStringLength(content, RightMargin(address, capacity));
        uint current = ReadMemoryWord(address);
        uint margin = ReadMemoryWord(AddAddress(address, 4));
        WriteMemory(AddAddress(address, checked(8 + margin + current)), content.AsSpan(0, count));
        WriteMemoryWord(address, checked(current + (uint)count));
    }

    private void HigaTuika()
    {
        mp.FetchMcodeInt();
        AppendStringLeft(AccessAddress);
    }

    private void HigaTuikaForJinfo()
    {
        (uint address, _) = ReadJinfo();
        AppendStringLeft(address);
    }

    private void AppendStringLeft(uint address)
    {
        uint length = dtp.PopUl();
        uint source = dtp.PopUl();
        byte[] content = ReadStringBytes(source, length);
        uint margin = ReadMemoryWord(AddAddress(address, 4));
        (int offset, int count) = FitStringSuffix(content, margin);
        if (count == 0) return;

        uint newMargin = margin - (uint)count;
        WriteMemory(AddAddress(address, checked(8 + newMargin)), content.AsSpan(offset, count));
        WriteMemoryWord(address, checked(ReadMemoryWord(address) + (uint)count));
        WriteMemoryWord(AddAddress(address, 4), newMargin);
    }

    private void FillSpace()
    {
        FillSpace(AccessAddress, mp.FetchMcodeInt());
    }

    private void FillSpaceForJinfo()
    {
        (uint address, uint capacity) = ReadJinfo();
        FillSpace(address, capacity);
    }

    private void FillSpace(uint address, uint capacity)
    {
        uint originalLength = ReadMemoryWord(address);
        uint originalMargin = ReadMemoryWord(AddAddress(address, 4));
        long rightMargin = (long)capacity - originalMargin - originalLength;
        uint length = originalLength;
        if (originalMargin != 0)
        {
            WriteMemory(AddAddress(address, 8), Enumerable.Repeat((byte)' ', checked((int)originalMargin)).ToArray());
            length = checked(length + originalMargin);
            WriteMemoryWord(AddAddress(address, 4), 0);
        }

        if (rightMargin > 0)
        {
            uint start = AddAddress(address, checked(8 + originalMargin + originalLength));
            WriteMemory(start, Enumerable.Repeat((byte)' ', checked((int)rightMargin)).ToArray());
            length = checked(length + (uint)rightMargin);
        }
        WriteMemoryWord(address, length);
    }

    private void MigiYose()
    {
        RightAlignString(AccessAddress, mp.FetchMcodeInt());
    }

    private void MigiYoseForJinfo()
    {
        (uint address, uint capacity) = ReadJinfo();
        RightAlignString(address, capacity);
    }

    private void SukosiMigiYose()
    {
        uint capacity = mp.FetchMcodeInt();
        uint shift = PopIntegerOfQuad();
        if (shift == 0) return;
        RightAlignString(AccessAddress, capacity, shift);
    }

    private void SukosiMigiYoseForJinfo()
    {
        (uint address, uint capacity) = ReadJinfo();
        uint shift = PopIntegerOfQuad();
        if (shift == 0) return;
        RightAlignString(address, capacity, shift);
    }

    private void RightAlignString(uint address, uint capacity, uint requestedShift = 0)
    {
        uint length = ReadMemoryWord(address);
        uint margin = ReadMemoryWord(AddAddress(address, 4));
        uint available = RightMargin(address, capacity);
        uint shift = requestedShift == 0 ? available : Math.Min(available, requestedShift);
        byte[] content = ReadStringBytes(AddAddress(address, checked(8 + margin)), length);
        WriteMemory(AddAddress(address, checked(8 + margin + shift)), content);
        WriteMemoryWord(AddAddress(address, 4), checked(margin + shift));
    }

    private void HidariYose()
    {
        mp.FetchMcodeInt();
        LeftAlignString(AccessAddress);
    }

    private void HidariYoseForJinfo()
    {
        (uint address, _) = ReadJinfo();
        LeftAlignString(address);
    }

    private void SukosiHidariYose()
    {
        mp.FetchMcodeInt();
        uint shift = PopIntegerOfQuad();
        if (shift == 0) return;
        LeftAlignString(AccessAddress, shift);
    }

    private void SukosiHidariYoseForJinfo()
    {
        (uint address, _) = ReadJinfo();
        uint shift = PopIntegerOfQuad();
        if (shift == 0) return;
        LeftAlignString(address, shift);
    }

    private void LeftAlignString(uint address, uint requestedShift = 0)
    {
        uint length = ReadMemoryWord(address);
        uint margin = ReadMemoryWord(AddAddress(address, 4));
        uint shift = requestedShift == 0 ? margin : Math.Min(margin, requestedShift);
        if (length != 0 && shift != 0)
        {
            byte[] content = ReadStringBytes(AddAddress(address, checked(8 + margin)), length);
            WriteMemory(AddAddress(address, checked(8 + margin - shift)), content);
        }
        WriteMemoryWord(AddAddress(address, 4), margin - shift);
    }

    private void JittaiCountSettei()
    {
        mp.FetchMcodeInt();
        ap.SetUl(PopIntegerOfQuad());
    }

    private void JittaiCountSetteiForJinfo()
    {
        (uint address, _) = ReadJinfo();
        WriteMemoryWord(address, PopIntegerOfQuad());
    }

    private void JittaiOffsetSettei()
    {
        mp.FetchMcodeInt();
        WriteMemoryWord(AddAddress(AccessAddress, 4), PopIntegerOfQuad());
    }

    private void JittaiOffsetSetteiForJinfo()
    {
        (uint address, _) = ReadJinfo();
        WriteMemoryWord(AddAddress(address, 4), PopIntegerOfQuad());
    }

    private void OoMojiHenkan()
    {
        mp.FetchMcodeInt();
        ConvertUppercaseString(AccessAddress);
    }

    private void OoMojiHenkanForJinfo()
    {
        (uint address, _) = ReadJinfo();
        ConvertUppercaseString(address);
    }

    private void ConvertUppercaseString(uint address)
    {
        uint length = ReadMemoryWord(address);
        uint margin = ReadMemoryWord(AddAddress(address, 4));
        uint stringAddress = AddAddress(address, checked(8 + margin));
        byte[] content = ReadStringBytes(stringAddress, length);
        for (int index = 0; index < content.Length;)
        {
            if (content.Length - index >= 2 && IsThisByteKanji(content[index]))
            {
                ushort character = (ushort)((content[index] << 8) | content[index + 1]);
                if (character is >= 0x8281 and <= 0x829A)
                {
                    character -= 0x21;
                    content[index] = (byte)(character >> 8);
                    content[index + 1] = (byte)character;
                }
                index += 2;
            }
            else
            {
                if (content[index] is >= (byte)'a' and <= (byte)'z')
                    content[index] -= 0x20;
                index++;
            }
        }
        WriteMemory(stringAddress, content);
    }
}
