using System.Text;

namespace Mind8Kernel
{
    public partial class Dispatcher
    {

        /*--------------------- 表示系  --------------------------------*/
        private void ZzHyouji()                	/* ;WORD ＄＄表示 */
        {
                /* Mコード=0x00C1 */
            // ULONG	count;
            // UCHAR  *addr;
            // count = POP_C;
            // addr  = POP_A;
            // #define  POP_U                ((ULONG)(*DstackPointer++))
            // #define  POP_A                ((UCHAR *)(*DstackPointer++))
            // #define  POP_C                POP_U
            // printStrinf( addr, count );
            uint count=dtp.PopUl();
            uint index=dtp.PopUl();
            byte[] chars=mp.GetUb(index,count);
            string text = Encoding.GetEncoding("shift_jis").GetString(chars);
            Console.Write(text);

        }
    }
}