namespace Mind8Kernel
{ 
 /// <summary>スタックポインタ</summary>
    public class StackPointer{
        private readonly uint stackSize=4;
        private uint stackMaxSize;
        private uint stackIndex;
        private uint stackPointer;
        public byte[] dataArray =[];
        public void SetupStackArray(uint size){
            stackMaxSize=size;
            dataArray =new byte[stackMaxSize];
            stackPointer=stackMaxSize/stackSize;//stackPointerは1024が初期値
            stackIndex=0;//stackIndexは0が初期値
        }        
        public void ResetStackPointer(){
            stackPointer=stackMaxSize/stackSize;
            stackIndex=0;//byte配列インデックス=アドレスポインタ変数の値*4
        }
        public void MoveStackPointer(int point){
            stackPointer=(uint)(stackPointer+point);
            stackIndex=(uint)(stackIndex - point*stackSize);//byte配列インデックス=アドレスポインタ変数の値*4
        }
        public uint GetCurrentStackPointer(){
            return stackPointer;
        }
        public ulong ReadAddress(long address, int size){
            ulong value = 0;
            for (int i = 0; i < size; i++){
                long location = checked(address + i);
                long index = (long)stackMaxSize - (location / 4) * 4 + location % 4;
                if (location < 0 || index < 0 || index >= dataArray.Length)
                    throw new ArgumentOutOfRangeException(nameof(address), "Stack address is outside the stack.");
                value |= (ulong)dataArray[index] << (8 * i);
            }
            return value;
        }
        public void WriteAddress(long address, ulong value, int size){
            for (int i = 0; i < size; i++){
                long location = checked(address + i);
                long index = (long)stackMaxSize - (location / 4) * 4 + location % 4;
                if (location < 0 || index < 0 || index >= dataArray.Length)
                    throw new ArgumentOutOfRangeException(nameof(address), "Stack address is outside the stack.");
                dataArray[index] = (byte)(value >> (8 * i));
            }
        }
        public uint[] CaptureDebugValues(){
            int depth = checked((int)(stackMaxSize / stackSize - stackPointer));
            var result = new uint[depth];
            for (int i = 0; i < depth; i++)
                result[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                    dataArray.AsSpan(checked((int)stackIndex - i * 4), 4));
            return result;
        }
        public bool IsCurrentStackPointerInit(){
            return (stackIndex==0);
        }
        public byte GetUb(){
            //アドレス指定で直接参照している元コードのためポインタインデックスの自動減少はなし
            byte[] int32Byte = new byte[stackSize];
            for (ulong i = 0; i < stackSize; i++) int32Byte[i] = dataArray[stackIndex + i];
            return (byte)BitConverter.ToUInt32(int32Byte, 0);            
        }
        public uint GetUl(){
            //アドレス指定で直接参照している元コードのためポインタインデックスの自動減少はなし
            byte[] int32Byte = new byte[stackSize];
            for (ulong i = 0; i < stackSize; i++) int32Byte[i] = dataArray[stackIndex + i];
            return BitConverter.ToUInt32(int32Byte, 0);            
        }
        public uint GetUlOffset(int offset){/* for unsigned long access  */
            //アドレス指定で直接セットしている元コードのためポインタインデックスの自動増加はなし
            byte[] int32Byte = new byte[stackSize];
            uint index=0;
            long requestedIndex = (long)stackIndex - (long)offset * stackSize;
            if(requestedIndex >= 0 && requestedIndex + stackSize <= dataArray.Length){
                index=(uint)requestedIndex;
            }
            for (ulong i = 0; i < stackSize; i++) int32Byte[i] = dataArray[index + i];
            return BitConverter.ToUInt32(int32Byte, 0);      
        }
        public uint GetUlPoint(int point){/* for unsigned long access  */
            //アドレス指定で直接セットしている元コードのためポインタインデックスの自動増加はなし
            byte[] int32Byte = new byte[stackSize];
            uint index=(uint)(stackMaxSize/stackSize-point)*stackSize;//+は減方向
            for (ulong i = 0; i < stackSize; i++) int32Byte[i] = dataArray[index + i];
            return BitConverter.ToUInt32(int32Byte, 0);      
        }
        private int GetByteOffsetIndex(int offset){
            long index = (long)stackIndex - (offset / 4L) * stackSize + offset % 4;
            if(offset < 0 || index < 0 || index >= dataArray.Length){
                throw new ArgumentOutOfRangeException(nameof(offset), "Return stack byte offset is outside the stack.");
            }
            return (int)index;
        }
        public uint ReadLocal(int offset, int size){
            uint value = 0;
            for(int i = 0; i < size; i++){
                value |= (uint)dataArray[GetByteOffsetIndex(checked(offset + i))] << (8 * i);
            }
            return value;
        }
        public void WriteLocal(int offset, uint value, int size){
            for(int i = 0; i < size; i++){
                dataArray[GetByteOffsetIndex(checked(offset + i))] = (byte)(value >> (8 * i));
            }
        }
        public byte PopUb(){    /* for unsigned byte access */
            if(stackIndex < stackSize)throw new InvalidOperationException("Stack underflow.");
            byte[] int32Byte = new byte[stackSize];
            for (ulong i = 0; i < stackSize; i++) int32Byte[i] = dataArray[stackIndex + i];
            stackPointer++;//POPはポインタ増加
            stackIndex-=stackSize;//POPはインデックス減少
            return (byte)BitConverter.ToUInt32(int32Byte, 0);
        }
        public ushort PopUs(){    /* for unsigned byte access */
            if(stackIndex < stackSize)throw new InvalidOperationException("Stack underflow.");
            byte[] int32Byte = new byte[stackSize];
            for (ulong i = 0; i < stackSize; i++) int32Byte[i] = dataArray[stackIndex + i];
            stackPointer++;//POPはポインタ増加
            stackIndex-=stackSize;//POPはインデックス減少
            return BitConverter.ToUInt16(int32Byte, 0);
        }
        public uint PopUl(){/* for unsigned long(int32) access   */
            if(stackIndex < stackSize)throw new InvalidOperationException("Stack underflow.");
            byte[] int32Byte = new byte[stackSize];
            for (ulong i = 0; i < stackSize; i++) int32Byte[i] = dataArray[stackIndex + i];
            stackPointer++;//POPはポインタ増加
            stackIndex-=stackSize;//POPはインデックス減少
            return BitConverter.ToUInt32(int32Byte, 0);
        }
        
        public void PushUb(byte b){    /* for unsigned byte access */
            if(stackIndex + stackSize > stackMaxSize-1)throw new Exception("stack over flow");
            stackPointer--;//PUSHはポインタ減少
            stackIndex +=stackSize;//PUSHはインデックス増加
            byte[] int32Byte = new byte[stackSize];
            int32Byte[3] =b;
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i]=int32Byte[i];
        }
        public void PushUs(ushort us){    /* for unsigned byte access */
            if(stackIndex + stackSize > stackMaxSize-1)throw new Exception("stack over flow");
            stackPointer--;//PUSHはポインタ減少
            stackIndex +=stackSize;//PUSHはインデックス増加
            byte[] int32Byte =BitConverter.GetBytes(us);
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i]=int32Byte[i];
        }
        public void PushUl(uint ul){/* for unsigned long(int32) access  */
            if(stackIndex + stackSize > stackMaxSize-1)throw new Exception("stack over flow");
            stackPointer--;//PUSHはポインタ減少
            stackIndex +=stackSize;//PUSHはインデックス増加           
            byte[] int32Byte =BitConverter.GetBytes(ul);
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i]=int32Byte[i];
        }
        public void PushSl(int sl){/* for signed long(int32) access  */
            if(stackIndex + stackSize > stackMaxSize-1)throw new Exception("stack over flow");
            stackPointer--;//PUSHはポインタ減少
            stackIndex +=stackSize;//PUSHはインデックス増加
            byte[] int32Byte =BitConverter.GetBytes(sl);
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i]=int32Byte[i];
        }
        public void SetSl2var(int ul,int ul2){/* for unsigned long access (for 1set of 2variable)  */
            //アドレス指定で直接セットしている元コードのためポインタインデックスの自動増加はなし
            byte[] int32Byte =BitConverter.GetBytes(ul);
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i]=int32Byte[i];
            int32Byte =BitConverter.GetBytes(ul2);
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i + 4]=int32Byte[i];
        }
        public void SetUs(ushort us){/* for unsigned short access  */
            //アドレス指定で直接セットしている元コードのためポインタインデックスの自動増加はなし
            byte[] int32Byte =BitConverter.GetBytes(us);
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i]=int32Byte[i];
        }
        public void SetUl(uint ul){/* for unsigned long access  */
            //アドレス指定で直接セットしている元コードのためポインタインデックスの自動増加はなし
            byte[] int32Byte =BitConverter.GetBytes(ul);
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i]=int32Byte[i];
        }
        public void SetUl2var(uint ul,uint ul2){/* for unsigned long access (for 1set of 2variable)  */
            //アドレス指定で直接セットしている元コードのためポインタインデックスの自動増加はなし
            byte[] int32Byte =BitConverter.GetBytes(ul);
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i]=int32Byte[i];
            int32Byte =BitConverter.GetBytes(ul2);
            for (ulong i = 0; i < stackSize; i++) dataArray[stackIndex + i + 4]=int32Byte[i];
        }
        public void SetUsOffset(ushort us,int offset){/* for unsigned short access  */
            //アドレス指定で直接セットしている元コードのためポインタインデックスの自動増加はなし
            byte[] int32Byte =BitConverter.GetBytes(us);
            uint index=(uint)(stackIndex - offset*stackSize);//+は減方向
            for (ulong i = 0; i < stackSize; i++) dataArray[index + i]=int32Byte[i];
        }
        public void SetUlOffset(uint ul,int offset){/* for unsigned long access  */
            //アドレス指定で直接セットしている元コードのためポインタインデックスの自動増加はなし
            byte[] int32Byte =BitConverter.GetBytes(ul);
            uint index=(uint)(stackIndex - offset*stackSize);//+は減方向
            for (ulong i = 0; i < stackSize; i++) dataArray[index + i]=int32Byte[i];
        }
        public void SetUlPoint(uint ul,int point){/* for unsigned long access  */
            //アドレス指定で直接セットしている元コードのためポインタインデックスの自動増加はなし
            byte[] int32Byte =BitConverter.GetBytes(ul);
            uint index=(uint)(stackMaxSize/stackSize-point)*stackSize;//+は減方向
            for (ulong i = 0; i < stackSize; i++) dataArray[index + i]=int32Byte[i];
        }
    }
}