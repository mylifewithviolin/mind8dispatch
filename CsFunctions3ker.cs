using System.Runtime.InteropServices;
using System.Text;

namespace Mind8Kernel
{
    public partial class Dispatcher
    {
        /*--------------------- 私のプログラム名  -----------------------*/
        private void CmcodeFullFilename(){		/* ;WORD c_mcodeFullFilename */
                /* Mコード=0x0261 */
            //PUSH_S( FullFilename, 0 );
            //#define  PUSH_S(addr,count)   PUSH_A(addr),PUSH_C(count)
            // #define  PUSH_L(value)        *(--DstackPointer)=(LONG)(value)
            // #define  PUSH_U(value)        PUSH_L(value)
            // #define  PUSH_A(addr)         PUSH_U(addr)
            // #define  PUSH_C(count)        PUSH_U(count)
            IntPtr ptr =Marshal.StringToHGlobalAnsi(mcodefilename);
            dtp.PushUl((uint)ptr);//暫定
            dtp.PushUl(0);
        }
        private void GetArgC()		/* ;WORD ａｒｇｃ */
        {
            /* Mコード=0x0264 */
            /* (. →　argc) */
            //PUSH_Q( Argc, 0 );
            dtp.PushUl((uint)argc);//暫定
            dtp.PushUl(0);
        }
        /*--------------------- 標準入出力  -----------------------------*/
        private void OutputDevice()	/* ;WORD ファイルハンドル指定で出力 */
        {
                /* Mコード=0x026D */  
            /* (strinfo, filehandle → .) */
            //     int		 filehandle;
            //     size_t		 length;
            //     UCHAR		*string;
            //#define  POP_U                ((ULONG)(*DstackPointer++))
            //#define  POP_U_OF_Q           (DROP_F,POP_U)
            // filehandle = POP_U_OF_Q;
            // length = POP_C;
            // string = POP_A;
            // write( filehandle, string, length );	/* エラーは見ない */
            dtp.PopUl();
            uint filehandle=dtp.PopUl();//捨てる
            uint length=dtp.PopUl();
            uint index=dtp.PopUl();              
            byte[] chars=mp.GetUb(index,length);
            string text = Encoding.GetEncoding("shift_jis").GetString(chars);
            Console.Write(text);
        }

    }
}