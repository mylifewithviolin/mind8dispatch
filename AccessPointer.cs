namespace Mind8Kernel
{
    public class AccessPointer
    {
        private byte[] dataArray = [];
        private uint address;
        internal StackPointer? Stack { get; private set; }
        internal MCodePointer? Code { get; private set; }
        internal uint Address => address;

        public void SetupDataArray(uint size)
        {
            dataArray = new byte[size];
            ResetDataIndex(0);
        }

        public void ResetDataIndex(ulong index)
        {
            address = checked((uint)index);
            Stack = null;
            Code = null;
        }

        public void PointStack(StackPointer pointer, int byteOffset) =>
            PointStackAddress(pointer, checked((uint)((long)pointer.GetCurrentStackPointer() * 4 + byteOffset)));

        internal void PointStackAddress(StackPointer pointer, uint location)
        {
            ResetDataIndex(location);
            Stack = pointer;
        }

        internal void PointCode(MCodePointer pointer, uint location)
        {
            ResetDataIndex(location);
            Code = pointer;
        }

        public byte[] CaptureDebugData() => (byte[])dataArray.Clone();

        public byte[] GetBytes(uint index, uint count)
        {
            ValidateDataRange(index, count);
            return dataArray.AsSpan((int)index, (int)count).ToArray();
        }

        internal void WriteDataBytes(uint index, ReadOnlySpan<byte> bytes)
        {
            ValidateDataRange(index, checked((uint)bytes.Length));
            bytes.CopyTo(dataArray.AsSpan((int)index));
        }

        private void ValidateDataRange(uint index, uint count)
        {
            if (index > dataArray.Length || count > dataArray.Length - index)
                throw new ArgumentOutOfRangeException(nameof(index), "Requested data byte range is outside the data area.");
        }

        public void ClearBytes(uint index, uint count)
        {
            ValidateDataRange(index, count);
            dataArray.AsSpan((int)index, (int)count).Clear();
        }

        public uint GetCurrentDataIndex()
        {
            if (Stack is not null || Code is not null)
                throw new InvalidOperationException("The access pointer does not refer to the data area.");
            return address;
        }

        private ulong ReadValue(int size)
        {
            if (Stack is not null) return Stack.ReadAddress(address, size);
            byte[] bytes = Code is not null ? Code.GetUb(address, (uint)size) : GetBytes(address, (uint)size);
            ulong value = 0;
            for (int i = 0; i < size; i++) value |= (ulong)bytes[i] << (8 * i);
            return value;
        }

        private void WriteValue(ulong value, int size)
        {
            if (Stack is not null) { Stack.WriteAddress(address, value, size); return; }
            Span<byte> bytes = stackalloc byte[8];
            for (int i = 0; i < size; i++) bytes[i] = (byte)(value >> (8 * i));
            if (Code is not null) Code.WriteBytes(address, bytes[..size]);
            else WriteDataBytes(address, bytes[..size]);
        }

        public byte GetUb() => (byte)ReadValue(1);
        public ushort GetUs() => (ushort)ReadValue(2);
        public short GetSs() => unchecked((short)ReadValue(2));
        public uint GetUl() => (uint)ReadValue(4);
        public int GetSl() => unchecked((int)ReadValue(4));
        public ulong GetUll() => ReadValue(8);
        public long GetSll() => unchecked((long)ReadValue(8));
        public void SetUb(byte value) => WriteValue(value, 1);
        public void SetUs(ushort value) => WriteValue(value, 2);
        public void SetSs(short value) => WriteValue(unchecked((ushort)value), 2);
        public void SetUl(uint value) => WriteValue(value, 4);
        public void SetSl(int value) => WriteValue(unchecked((uint)value), 4);
        public void SetUll(ulong value) => WriteValue(value, 8);
        public void SetSll(long value) => WriteValue(unchecked((ulong)value), 8);
        public void SetUl4Ms(uint first, uint second) => SetUll(first | ((ulong)second << 32));
        public void SetUl2var(uint first, uint second) => SetUl4Ms(first, second);

        public void SetUb4Msi(byte value, uint length)
        {
            for (uint i = 0; i < length; i++)
            {
                uint target = checked(address + i);
                if (Stack is not null) Stack.WriteAddress(target, value, 1);
                else if (Code is not null) Code.WriteBytes(target, new byte[] { value });
                else
                {
                    ValidateDataRange(target, 1);
                    dataArray[target] = value;
                }
            }
        }
    }
}
