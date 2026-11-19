namespace Mind8Kernel
{
    public partial class Dispatcher
    {
        private void IsKuretu()
        {
            uint length = dtp.PopUl();
            dtp.PopUl();
            dtp.PushUl(length == 0 ? 1u : 0u);
            dtp.PushUl(0);
        }

        private void IsNotKuretu()
        {
            IsKuretu();
            IsFalse();
        }

        private static bool IsThisByteKanji(byte value) =>
            value is >= 0x81 and <= 0x9F or >= 0xE0;

        private void ZzCtoS()
        {
            ushort character = (ushort)dtp.GetUlOffset(1);
            uint length = character > 255 ? 2u : 1u;
            if (length == 2)
                dtp.SetUlOffset((uint)((character << 8) | (character >> 8)) & 0xFFFF, 1);
            uint address = AddAddress(DataStackAddressBit | checked(dtp.GetCurrentStackPointer() * 4), 4);
            dtp.PushUl(address);
            dtp.PushUl(length);
        }

        private static int FitStringLength(byte[] bytes, uint capacity)
        {
            int length = bytes.Length;
            while ((uint)length > capacity)
                length -= LastCharacterWidth(bytes, length);
            return length;
        }

        private void PopJvar_0(uint address, uint capacity)
        {
            uint length = dtp.PopUl();
            uint source = dtp.PopUl();
            byte[] content = ReadStringBytes(source, length);
            int count = FitStringLength(content, capacity);
            WriteMemory(AddAddress(address, 8), content.AsSpan(0, count));
            WriteMemoryWord(address, (uint)count);
            WriteMemoryWord(AddAddress(address, 4), 0);
        }

        private void ZzPopJvar() => PopJvar_0(AccessAddress, mp.FetchMcodeInt());

        private void ZzWriteJvar()
        {
            uint address = DataAddressBit | mp.FetchMcodeInt();
            PopJvar_0(address, mp.FetchMcodeInt());
        }

        private void ZzWriteJvarLocal()
        {
            uint address = LocalAddress(mp.FetchMcode());
            PopJvar_0(address, mp.FetchMcode());
        }

        private void MojiretuJittaiWoIre()
        {
            uint capacity = dtp.PopUl();
            uint address = dtp.PopUl();
            PopJvar_0(address, capacity);
        }

        private void ZzClearJvar()
        {
            dtp.PopUl();
            uint address = dtp.PopUl();
            WriteMemoryWord(address, 0);
            WriteMemoryWord(AddAddress(address, 4), 0);
        }

        private void PushJvarString(uint address)
        {
            uint length = ReadMemoryWord(address);
            uint margin = ReadMemoryWord(AddAddress(address, 4));
            dtp.PushUl(AddAddress(address, checked(8 + margin)));
            dtp.PushUl(length);
        }

        private void MojiretuJittaiWoSansyo()
        {
            dtp.PopUl();
            PushJvarString(dtp.PopUl());
        }

        private void ChgDefaultStruct()
        {
            uint capacity = dtp.PopUl();
            uint address = dtp.PopUl();
            if (dtp.GetUlOffset(1) == 0)
            {
                dtp.SetUlOffset(address, 1);
                dtp.SetUl(capacity);
            }
        }
    }
}
