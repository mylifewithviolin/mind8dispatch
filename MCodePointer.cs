    
namespace Mind8Kernel
{
    /// <summary>MCode�̈�A�N�Z�X�|�C���^</summary>
    public class MCodePointer{
        private readonly uint mcodeSize=2;
        private readonly uint mcodeSizeDouble=4;
        private uint mcodeIndex;
        private byte[] mcodeArray =[];
        public void SetupMcodeArray(byte[] McodeBase){
            mcodeArray=McodeBase;
        }
        public void ResetMcodeIndex(uint index){
            mcodeIndex=index;;
        }
        public void MoveMcodeIndexByByte(int point){
            mcodeIndex=(uint)(mcodeIndex + point);//byte�z��C���f�b�N�X�P�ʂňړ�
        }
        public void MoveMcodeIndex(int point){
            mcodeIndex=(uint)(mcodeIndex + point*mcodeSize);//byte�z��C���f�b�N�X=�A�h���X�|�C���^�ϐ��̒l*2
        }
        public uint GetMcodeIndex(){
            return mcodeIndex;
        }
        public uint AppendBytes(ReadOnlySpan<byte> bytes){
            int index = mcodeArray.Length;
            Array.Resize(ref mcodeArray, checked(index + bytes.Length));
            bytes.CopyTo(mcodeArray.AsSpan(index));
            return (uint)index;
        }
        public ushort ReadMcode(){
            byte[] int16Byte = new byte[mcodeSize];
            for (ulong i = 0; i < mcodeSize; i++) int16Byte[i] = mcodeArray[mcodeIndex + i];
            return BitConverter.ToUInt16(int16Byte, 0);
        }
         public short ReadMcodeShort(){  /* FETCH_MCODE_SHORT */
            byte[] int16Byte = new byte[mcodeSize];
            for (ulong i = 0; i < mcodeSize; i++) int16Byte[i] = mcodeArray[mcodeIndex + i];
            return BitConverter.ToInt16(int16Byte, 0);
        }       
        public ushort FetchMcode(){ /* FETCH_MCODE */
            byte[] int16Byte = new byte[mcodeSize];
            for (ulong i = 0; i < mcodeSize; i++) int16Byte[i] = mcodeArray[mcodeIndex + i];
            mcodeIndex +=2;
            return BitConverter.ToUInt16(int16Byte, 0);
        }
        public uint FetchMcodeInt(){ /* FETCH_MCODE_LONG */
            byte[] int32Byte = new byte[mcodeSizeDouble];
            for (ulong i = 0; i < mcodeSizeDouble; i++) int32Byte[i] = mcodeArray[mcodeIndex + i];
            mcodeIndex +=mcodeSizeDouble;
            return BitConverter.ToUInt32(int32Byte, 0);
        }
        public ushort GetMCodeCurrentIndex(){/* for unsigned short access  */
            byte[] int16Byte = new byte[mcodeSize];
            for (ulong i = 0; i < mcodeSize; i++) int16Byte[i] = mcodeArray[mcodeIndex + i];
            mcodeIndex +=2;
            return BitConverter.ToUInt16(int16Byte, 0);
        }
        public byte[] GetUb(uint index,uint count){
            if (index > mcodeArray.Length || count > mcodeArray.Length - index)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Requested M-code byte range is outside the loaded data.");
            }

            byte[] chars = new byte[count];
            for (uint i = 0; i < count; i++) chars[i] = mcodeArray[index + i];
            return chars;
        }
        public uint SearchByteDirectForward(uint index, uint count, byte target){
            for (uint position = 0; position < count; position++)
            {
                uint currentIndex = checked(index + position);
                if (currentIndex >= mcodeArray.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(count), "Requested M-code search exceeds the loaded data.");
                }

                if (mcodeArray[currentIndex] == target)
                {
                    return position + 1;
                }
            }

            return 0;
        }
        public short GetSs(){/* for signed short access  */
            byte[] int16Byte = new byte[mcodeSize];
            for (ulong i = 0; i < mcodeSize; i++) int16Byte[i] = mcodeArray[mcodeIndex + i];
            mcodeIndex +=2;
            return BitConverter.ToInt16(int16Byte, 0);
        }
        public int GetSl(){/* for signed long access   */
            byte[] int32Byte = new byte[4];
            for (ulong i = 0; i < 4; i++) int32Byte[i] = mcodeArray[mcodeIndex + i];
            mcodeIndex +=4;
            return BitConverter.ToInt32(int32Byte, 0);
        }
        public uint GetUl(){/* for unsigned long access   */
            return FetchMcodeInt();
        }
        public long GetSll(){/* for signed long long access   */
            byte[] int64Byte = new byte[8];
            for (ulong i = 0; i < 8; i++) int64Byte[i] = mcodeArray[mcodeIndex + i];
            mcodeIndex +=8;
            return BitConverter.ToInt64(int64Byte, 0);
        }
        public ulong GetUll(){/* for unsigned long long access   */
            byte[] int64Byte = new byte[8];
            for (ulong i = 0; i < 8; i++) int64Byte[i] = mcodeArray[mcodeIndex + i];
            mcodeIndex +=8;
            return BitConverter.ToUInt64(int64Byte, 0);
        }
    }
}