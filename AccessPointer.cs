    namespace Mind8Kernel
{
    /// <summary>データ領域アクセスポインタ</summary>
    public class AccessPointer{
        private uint dataSize;
        private ulong dataIndex;
        private byte[] dataArray =[];
        private StackPointer? stack;
        private long stackAddress;
        public void SetupDataArray(uint size){
            dataSize=size;
            dataArray =new byte[dataSize];
            dataIndex=0;
            stack=null;
        }
        public void ResetDataIndex(ulong index){
            dataIndex=index;
            stack=null;
        }
        public void PointStack(StackPointer pointer, int byteOffset){
            stack=pointer;
            stackAddress=checked((long)pointer.GetCurrentStackPointer() * 4 + byteOffset);
        }
        public byte[] CaptureDebugData() => (byte[])dataArray.Clone();
        public byte[] GetBytes(uint index, uint count){
            if (index > dataArray.Length || count > dataArray.Length - index)
                throw new ArgumentOutOfRangeException(nameof(index), "Requested data byte range is outside the data area.");
            return dataArray.AsSpan(checked((int)index), checked((int)count)).ToArray();
        }
        public void ClearBytes(uint index, uint count){
            if (index > dataArray.Length || count > dataArray.Length - index)
                throw new ArgumentOutOfRangeException(nameof(index), "Requested data byte range is outside the data area.");
            dataArray.AsSpan(checked((int)index), checked((int)count)).Clear();
        }
        public uint GetCurrentDataIndex(){
            if (stack is not null) throw new InvalidOperationException("The access pointer refers to the return stack, not the data area.");
            return (uint)dataIndex;
        }
        public byte GetUb(){    /* for unsigned byte access */
            if (stack is not null) return (byte)stack.ReadAddress(stackAddress, 1);
            return dataArray[dataIndex];
        }
        public ushort GetUs(){/* for unsigned short access  */
            if (stack is not null) return (ushort)stack.ReadAddress(stackAddress, 2);
            byte[] int16Byte = new byte[2];
            for (ulong i = 0; i < 2; i++) int16Byte[i] = dataArray[dataIndex + i];
            return BitConverter.ToUInt16(int16Byte, 0);
        }        
        public short GetSs(){/* for signed short access  */
            if (stack is not null) return unchecked((short)stack.ReadAddress(stackAddress, 2));
            byte[] int16Byte = new byte[2];
            for (ulong i = 0; i < 2; i++) int16Byte[i] = dataArray[dataIndex + i];
            return BitConverter.ToInt16(int16Byte, 0);
        }
        public int GetSl(){/* for signed long access   */
            if (stack is not null) return unchecked((int)stack.ReadAddress(stackAddress, 4));
            byte[] int32Byte = new byte[4];
            for (ulong i = 0; i < 4; i++) int32Byte[i] = dataArray[dataIndex + i];
            return BitConverter.ToInt32(int32Byte, 0);
        }
        public uint GetUl(){/* for unsigned long access   */
            if (stack is not null) return (uint)stack.ReadAddress(stackAddress, 4);
            byte[] int32Byte = new byte[4];
            for (ulong i = 0; i < 4; i++) int32Byte[i] = dataArray[dataIndex + i];

            return BitConverter.ToUInt32(int32Byte, 0);
        } 
        public long GetSll(){/* for signed long long access   */
            if (stack is not null) return unchecked((long)stack.ReadAddress(stackAddress, 8));
            byte[] int64Byte = new byte[8];
            for (ulong i = 0; i < 8; i++) int64Byte[i] = dataArray[dataIndex + i];
            return BitConverter.ToInt64(int64Byte, 0);
        }
        public ulong GetUll(){/* for unsigned long long access   */
            if (stack is not null) return stack.ReadAddress(stackAddress, 8);
            byte[] int64Byte = new byte[8];
            for (ulong i = 0; i < 8; i++) int64Byte[i] = dataArray[dataIndex + i];
            return BitConverter.ToUInt64(int64Byte, 0);
        }
        
        public void SetUb(byte b){    /* for unsigned byte access */
            if (stack is not null) { stack.WriteAddress(stackAddress, b, 1); return; }
            dataArray[dataIndex] =b;
        }
        public void SetUb4Msi(byte b,uint length){  /* for unsigned byte access (for mind strings instance)*/
            if (stack is not null) { for (uint i = 0; i < length; i++) stack.WriteAddress(stackAddress + i, b, 1); return; }
            for (ulong i = 0; i < length; i++) dataArray[dataIndex + i]= b;
        }

         public void SetUs(ushort us){/* for unsigned short access  */
            if (stack is not null) { stack.WriteAddress(stackAddress, us, 2); return; }
            byte[] int16Byte =BitConverter.GetBytes(us);
            for (ulong i = 0; i < 2; i++) dataArray[dataIndex + i]=int16Byte[i];
         }

        public void SetSs(short ss){/* for signed short access  */
            if (stack is not null) { stack.WriteAddress(stackAddress, unchecked((ushort)ss), 2); return; }
            byte[] int16Byte =BitConverter.GetBytes(ss);
            for (ulong i = 0; i < 2; i++) dataArray[dataIndex + i]=int16Byte[i];
         }

        public void SetSl(int sl){/* for signed long access  */
            if (stack is not null) { stack.WriteAddress(stackAddress, unchecked((uint)sl), 4); return; }
            byte[] int32Byte =BitConverter.GetBytes(sl);
            for (ulong i = 0; i < 4; i++) dataArray[dataIndex + i]=int32Byte[i];
        }
        public void SetUl(uint ul){/* for unsigned long access  */
            if (stack is not null) { stack.WriteAddress(stackAddress, ul, 4); return; }
            byte[] int32Byte =BitConverter.GetBytes(ul);
            for (ulong i = 0; i < 4; i++) dataArray[dataIndex + i]=int32Byte[i];
        }
        public void SetUl4Ms(uint ul,uint ul2){/* for unsigned long access (for mind string)  */
            if (stack is not null) { stack.WriteAddress(stackAddress, ul, 4); stack.WriteAddress(stackAddress + 4, ul2, 4); return; }
            byte[] int32Byte =BitConverter.GetBytes(ul);
            for (ulong i = 0; i < 4; i++) dataArray[dataIndex + i]=int32Byte[i];
            int32Byte =BitConverter.GetBytes(ul2);
            for (ulong i = 0; i < 4; i++) dataArray[dataIndex + i + 4]=int32Byte[i];
        }
        public void SetUl2var(uint ul,uint ul2){/* for unsigned long access (for 1set of 2variable)  */
            if (stack is not null) { stack.WriteAddress(stackAddress, ul, 4); stack.WriteAddress(stackAddress + 4, ul2, 4); return; }
            byte[] int32Byte =BitConverter.GetBytes(ul);
            for (ulong i = 0; i < 4; i++) dataArray[dataIndex + i]=int32Byte[i];
            int32Byte =BitConverter.GetBytes(ul2);
            for (ulong i = 0; i < 4; i++) dataArray[dataIndex + i + 4]=int32Byte[i];
        }
        public void SetSll(long sll){/* for signed long access  */
            if (stack is not null) { stack.WriteAddress(stackAddress, unchecked((ulong)sll), 8); return; }
            byte[] int64Byte =BitConverter.GetBytes(sll);
            for (ulong i = 0; i < 8; i++) dataArray[dataIndex + i]=int64Byte[i];
        }
        public void SetUll(ulong ull){/* for unsigned long access  */
            if (stack is not null) { stack.WriteAddress(stackAddress, ull, 8); return; }
            byte[] int64Byte =BitConverter.GetBytes(ull);
            for (ulong i = 0; i < 8; i++) dataArray[dataIndex + i]=int64Byte[i];
        }
    }
}