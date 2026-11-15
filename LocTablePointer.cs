using System.Security.Cryptography;
namespace Mind8Kernel
{
/// <summary>Locテーブルアクセスポインタ</summary>
public class LocTablePointer{
        private byte[] locArray =[];
        public void SetupLocArray(byte[] locTable){
            locArray=locTable;
        }
        public int GetSl(ulong locIndex){/* for signed long access   */
            locIndex*=4;
            byte[] int32Byte = new byte[4];
            for (ulong i = 0; i < 4; i++) int32Byte[i] = locArray[locIndex + i];
            return BitConverter.ToInt32(int32Byte, 0);
        }
        
    }
}