namespace Mind8Kernel
{
    public partial class Dispatcher
    {

        /*--------------------- ならば～つぎに  -------------------------*/    
        private void Zzif(){ /* ;WORD $$IF */
                /* Mコード=0x00EC */
            // #define  DROP_L               DstackPointer++
            // #define  DROP_F               DROP_L
            // #define  POP_L                (*DstackPointer++)
            // #define  POP_L_OF_Q           (DROP_F,POP_L)
            // if ( POP_L_OF_Q )
            // {
            //         SKIP_MCODE_1WORD;
            //#define  SKIP_MCODE_1WORD   McodePointer.w++
            // }
            // else
            // {
            //         SKIP_MCODE( READ_MCODE_SHORT );
            //#define  SKIP_MCODE(bytes)  McodePointer.b+=(bytes)
            //#define  READ_MCODE_SHORT   (*McodePointer.s)
            // }
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            if(dtp.PopUl()>=1){
                mp.MoveMcodeIndex(1);
            }else{
                mp.MoveMcodeIndexByByte(mp.ReadMcodeShort());
            }
        }
        private void ZzNotif(){ /* ;WORD $$-IF */
                /* Mコード=0x00ED */
            // if ( POP_L_OF_Q )
            // {
            //         SKIP_MCODE( READ_MCODE_SHORT );
            //#define  SKIP_MCODE(bytes)  McodePointer.b+=(bytes)
            //#define  READ_MCODE_SHORT   (*McodePointer.s)
            // }
            // else
            // {
            //         SKIP_MCODE_1WORD;
            // }
            //uint point=dtp.GetCurrentStackPointer();
            //for(uint i=point +5;i>point;i--)Console.WriteLine(" {0,8:d} {1,8:d}",i,dtp.dataArray[i*4]);
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            if(dtp.PopUl()>=1){
                //mp.MoveMcodeIndexByByte(mp.ReadMcodeShort());
                short mc=mp.ReadMcodeShort();
                mp.MoveMcodeIndexByByte(mc);
            }else{
                mp.MoveMcodeIndex(1);
            }
        }

        /*------------------- 回数指定～繰り返し  ------------------------*/

        private void ZzDo(){   /* ;WORD $$DO */

                    /* Mコード=0x00ED */
            //         ULONG   count;
            // /* (value → .) */
            // /*-[Mcode生成]-----------------*/
            // /*   +0(W): relative address    /   ←単位はバイト数(符号付き) */
            // /*         ($$LOOPの次へのjmp) */
            // /*-----------------------------*/
            // count = POP_L_OF_Q;                     /* カウントをpopする */
            // if ( !count )                           /* カウント=0 ? */
            // {
            //         SKIP_MCODE( READ_MCODE_SHORT ); /* $$LOOPの次の位置に飛ぶ */
            // }
            // else
            // {
            //         PUSH_R( count );                /* カウントを積む(long) */
            //         PUSH_R( 1 );                    /* 初期値(1)を積む(long) */
            //         SKIP_MCODE_1WORD;               /* $$LOOPへの飛び先をskip */
            // }
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる 
            uint count=dtp.PopUl();       
            if(count==0){
                //mp.MoveMcodeIndexByByte(mp.ReadMcodeShort());
                short mc=mp.ReadMcodeShort();
                mp.MoveMcodeIndexByByte(mc);
            }else{
                rtp.PushUl(count);
                rtp.PushUl(0);
                mp.MoveMcodeIndex(1);
            }
        }

    }
}