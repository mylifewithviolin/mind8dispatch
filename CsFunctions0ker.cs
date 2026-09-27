namespace Mind8Kernel
{
    public partial class Dispatcher
    {
        /*--------------------- 文字列情報分解・合成  -------------------*/
        private void MojiretuJyouhouBunkai(){	/* ;WORD 文字列情報分解 .S */
                /* Mコード=0x00BE */
            // UCHAR *addr;
            // ULONG count = POP_C;
            // addr  = POP_A;
            // PUSH_Q( addr, 0 );
            // PUSH_Q( count, 0 );
            uint count=dtp.PopUl();
            uint addr=dtp.PopUl();
            dtp.PushUl(addr);
            dtp.PushUl(0);
            dtp.PushUl(count);
            dtp.PushUl(0);
        }
        private void MojiretuJyouhouGousei(){	/* ;WORD 文字列情報合成 NN XSW */
                /* Mコード=0x00BF */
            // ULONG count = POP_U_OF_Q;
            // WRITE_C( count );
            // #define  WRITE_L(value)       *DstackPointer=(LONG)(value)
            // #define  WRITE_F(value)       WRITE_L(value)
            // #define  WRITE_C(count)       WRITE_F(count)
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            uint count=dtp.PopUl();
            dtp.SetUl(count);

        }

    }
}