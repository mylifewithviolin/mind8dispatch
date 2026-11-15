
using System.Dynamic;
using System.Runtime.InteropServices;

namespace Mind8Kernel
{
    public partial class Dispatcher
    {

        /*--------------------- 実行  -----------------------------------*/
        private void ZzEndExecute(){  /* ;WORD $$END_EXECUTE */
                /* Mコード=0x0010 */
            // DROP_R;				    /* 臨時のMコード域予約を解消 */
            // McodePointer.w = (USHORT *)POP_R;   /* '実行'直前のMポインタ復帰 */

            //#define  DROP_R             RstackPointer++
            rtp.PopUl();
            mp.ResetMcodeIndex((ushort)rtp.PopUl());
        }
        private void Jikkou(){       /* ;WORD 実行 */
                /* Mコード=0x0011 */
            // USHORT  *rp;
            // PUSH_R( McodePointer.u );	/* 現Ｍコードポインタを退避 */
            // rp = (USHORT *)--RstackPointer; /* リターンスタックに2wordを確保 */
            // McodePointer.w = rp;            /* その先頭から実行する仕掛け    */
            // *rp = POP_W_OF_Q;       	/* 指定されたMcodeを1ワード生成 */
            // *(rp+1) = 0x0010;               /* 後片付け処理のＭコードを記入 */
            //           /*↑'zzEndExecute'のMコード */
            rtp.PushUl(mp.GetMcodeIndex());
            ushort rp=(ushort)rtp.GetCurrentStackPointer();
            mp.ResetMcodeIndex(rp);
            dtp.PopUl();
            rtp.PushUl(dtp.PopUl());
            rtp.PushUl(0x0010);
        }
        private void Musyori(){      /* ;WORD 無処理 */
                /* Mコード=0x0012 */
                //無処理
        }
        private void EmgcyExit(){    /* ;WORD ＄＄実行終り・0001 */
                /* Mコード=0x0013 */
            //exit_program( 1 );
            throw new ExitModule(1);
        }
        private void ProcessOwari(){ /* ;WORD プロセス終り０ */
                /* Mコード=0x0014 */
            // LONG retCode;
            // retCode = POP_L_OF_Q;
            // exit_program( retCode );

            // #define  POP_L_OF_Q           (DROP_F,POP_L)
            dtp.PopUl();
            int retCode =(int)dtp.PopUl();
            throw new ExitModule(retCode);
        }

        /*--------------------- スタックポインタのアクセス  --------------*/
        private void StackKensa0(){/* ;WORD スタック検査０ */
                /* Mコード=0x0015 */
            // if ( DstackPointer == DataStackBase[RestartEnvCount] )
            // {
            //         /* OK */
            //         PUSH_Q( 1, 0 );
            // }
            // else
            // {
            //         /* NG */
            //         resetDataStackPointer();        /* in dispatch.c */
            //         PUSH_Q( 0, 0 );
            // }
            if(dtp.IsCurrentStackPointerInit()){
                dtp.PushUl(1);
                dtp.PushUl(0);
            }else{
                dtp.ResetStackPointer();
                dtp.PushUl(0);
                dtp.PushUl(0);
            }

        }
        private void StackReset(){      /* ;WORD スタックリセット */
                /* Mコード=0x0016 */
            // resetDataStackPointer();                /* in dispatch.c */
            // resetRetnStackPointer();                /* in dispatch.c */
            // longjmp( RESTARTENV, 1 );       	/* ディスパッチャ強制再開始 */
            dtp.ResetStackPointer();//DstackPointer = DataStackBase[RestartEnvCount];
            rtp.ResetStackPointer();//RstackPointer = RetnStackBase;
            throw new LongJmp(1);
        }
        private void FinishDispatcher(){	/* ;WORD ディスパッチャ終了 */
                /* Mコード=0x0017 */
            //longjmp( RESTARTENV, 2 );       	/* ディスパッチャ強制再開始 */
            throw new LongJmp(2);
        }
        private void C_stack_data_no(){  /* ;WORD スタックデータ数2 */
                /* Mコード=0x0018 */
            //longjmp( RESTARTENV, 2 );	       /* ディスパッチャ強制再開始 */
            throw new LongJmp(2);
        }

        /*--------------------- $$RESERVE[nL]  --------------------------*/
        private void ZzReserve1L(){         /* ;WORD $$RESERVE_1L */
                /* Mコード=0x0019 */
            //RstackPointer -= 1;
            rtp.MoveStackPointer(-1);
        }
        private void ZzReserve2L(){    /* ;DUMMYWORD */
                /* Mコード=0x001A */
            //RstackPointer -= 2;
            rtp.MoveStackPointer(-2);
        }
        private void ZzReserve3L(){    /* ;DUMMYWORD */
                /* Mコード=0x001B */
            //RstackPointer -= 3;
            rtp.MoveStackPointer(-3);
        }
        private void ZzReserve4L(){    /* ;DUMMYWORD */
                /* Mコード=0x001C */
            //RstackPointer -= 4;
            rtp.MoveStackPointer(-4);
        }
        private void ZzReserve5L(){    /* ;DUMMYWORD */
                /* Mコード=0x001D */       
            //RstackPointer -= 5;
            rtp.MoveStackPointer(-5);
        }
        private void ZzReserve6L(){    /* ;DUMMYWORD */
                /* Mコード=0x001E */
            //RstackPointer -= 6;
            rtp.MoveStackPointer(-6);     
        }
        private void ZzReserve(){      /* ;WORD $$RESERVE */
                /* Mコード=0x001F */     
            //RstackPointer -= FETCH_MCODE;
            rtp.MoveStackPointer(-1*mp.FetchMcode());
        }

        /*--------------------- $$EXIT[nL]  -----------------------------*/
        private void ZzExit1L(){ /* ;WORD $$EXIT_1L */
                /* Mコード=0x0020 */
            //McodePointer.w = (USHORT *)POP_R;

            //#define  POP_R              (*RstackPointer++)
            mp.ResetMcodeIndex(rtp.PopUl());
        }
        private void ZzExit2L(){   /* ;DUMMYWORD */
                /* Mコード=0x0021 */
            // RstackPointer += 2;
            // McodePointer.w = (USHORT *)(*(RstackPointer-1));
            rtp.MoveStackPointer(2);
            mp.ResetMcodeIndex(rtp.GetUlOffset(-1));
            //rtp.MoveStackPointer(-1);
            //mp.ResetMcodeIndex(rtp.PopUs()); 
        }
        private void ZzExit3L(){   /* ;DUMMYWORD */
                /* Mコード=0x0022 */
            // RstackPointer += 3;
            // McodePointer.w = (USHORT *)(*(RstackPointer-1));
            rtp.MoveStackPointer(3);
            mp.ResetMcodeIndex(rtp.GetUlOffset(-1));
            //rtp.MoveStackPointer(-1);
            //mp.ResetMcodeIndex(rtp.PopUs()); 
        }
        private void ZzExit4L(){   /* ;DUMMYWORD */
                /* Mコード=0x0023 */
            // RstackPointer += 4;
            // McodePointer.w = (USHORT *)(*(RstackPointer-1));
            rtp.MoveStackPointer(4);
            mp.ResetMcodeIndex(rtp.GetUlOffset(-1));
            //rtp.MoveStackPointer(-1);
            //mp.ResetMcodeIndex(rtp.PopUs()); ; 
        }
        private void ZzExit5L(){   /* ;DUMMYWORD */
                /* Mコード=0x0024 */
            // RstackPointer += 5;
            // McodePointer.w = (USHORT *)(*(RstackPointer-1));
            rtp.MoveStackPointer(5);
            mp.ResetMcodeIndex(rtp.GetUlOffset(-1));
            //rtp.MoveStackPointer(-1);
            //mp.ResetMcodeIndex(rtp.PopUs()); 
        }
        private void ZzExit6L(){   /* ;DUMMYWORD */
                /* Mコード=0x0025 */
            // RstackPointer += 6;
            // McodePointer.w = (USHORT *)(*(RstackPointer-1));
            rtp.MoveStackPointer(6);
            mp.ResetMcodeIndex(rtp.GetUlOffset(-1));
            //rtp.MoveStackPointer(-1);
            //mp.ResetMcodeIndex(rtp.PopUs()); 
        }
        private void ZzExit7L(){   /* ;DUMMYWORD */
                /* Mコード=0x0026 */
            // RstackPointer += 7;
            // McodePointer.w = (USHORT *)(*(RstackPointer-1));
            rtp.MoveStackPointer(7);
            mp.ResetMcodeIndex(rtp.GetUlOffset(-1));
            //rtp.MoveStackPointer(-1);
            //mp.ResetMcodeIndex(rtp.PopUs()); 
        }
        private void ZzExit(){
                /* Mコード=0x0027 */
            // RstackPointer += FETCH_MCODE;
            // McodePointer.w = (USHORT *)(*(RstackPointer-1));
            rtp.MoveStackPointer(mp.FetchMcode());
            rtp.MoveStackPointer(-1);
            mp.ResetMcodeIndex(rtp.PopUs()); 
        }

        /*--------------------- $$IFEXIT系  -----------------------------*/
        private void ZzIfExit(){       /* ;WORD $$IFEXIT */
                /* Mコード=0x0028 */
            // if ( POP_L_OF_Q )
            // {
            //         zzExit();
            // }
            // else
            // {
            //         SKIP_MCODE_1WORD;
            //#define  SKIP_MCODE_1WORD   Mcode
            // }
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            if(dtp.PopUl()>=1){
                ZzExit();
            }else{
                mp.MoveMcodeIndex(1);
            }
        }
        private void ZzIf0Exit(){  /* ;DUMMYWORD */
                /* Mコード=0x0029 */
            // if ( POP_L_OF_Q )
            // {
            //         PUSH_Q( 0, 0 );                 /* 00000000.00000000を積む */
            //         zzExit();
            // }
            // else
            // {
            //         SKIP_MCODE_1WORD;
            // }
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            if(dtp.PopUl()>=1){
                dtp.PushUl(0);
                dtp.PushUl(0);
                ZzExit();
            }else{
                mp.MoveMcodeIndex(1);
            }
        }
        private void ZzIf1Exit(){  /* ;DUMMYWORD */
                /* Mコード=0x002A */
            // if ( POP_L_OF_Q )
            // {
            //         PUSH_Q( 1, 0 );                 /* 00000001.00000000を積む */
            // #define  PUSH_L(value)        *(--DstackPointer)=(LONG)(value)
            // #define  PUSH_F(value)        PUSH_L(value)
            // #define  PUSH_Q(i,f)          PUSH_L(i),PUSH_F(f)
            //         zzExit();
            // }
            // else
            // {
            //         SKIP_MCODE_1WORD;
            // }
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            if(dtp.PopUl()>=1){
                dtp.PushUl(1);
                dtp.PushUl(0);
                ZzExit();
            }else{
                mp.MoveMcodeIndex(1);
            }
        }
        private void ZzNotIfExit(){  /* ;DUMMYWORD */
                /* Mコード=0x002B */
            // if ( POP_L_OF_Q )
            // {
            //         SKIP_MCODE_1WORD;
            // }
            // else
            // {
            //         zzExit();
            // }
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            if(dtp.PopUl()>=1){
                mp.MoveMcodeIndex(1);
            }else{
                ZzExit();
            }
        }
        private void ZzNotIf0Exit(){  /* ;DUMMYWORD */
                /* Mコード=0x002C */
            // if ( POP_L_OF_Q )
            // {
            //         SKIP_MCODE_1WORD;
            // }
            // else
            // {
            //         PUSH_Q( 0, 0 );                 /* 00000000.00000000を積む */
            //         zzExit();
            // }
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            if(dtp.PopUl()>=1){
                mp.MoveMcodeIndex(1);
            }else{
                dtp.PushUl(0);
                dtp.PushUl(0);
                ZzExit();
            }
        }
        private void ZzNotIf1Exit(){  /* ;DUMMYWORD */
                /* Mコード=0x002D */
            // if ( POP_L_OF_Q )
            // {
            //         SKIP_MCODE_1WORD;
            // }
            // else
            // {
            //         PUSH_Q( 1, 0 );                 /* 00000001.00000000を積む */
            //         zzExit();
            // }
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            if(dtp.PopUl()>=1){
                mp.MoveMcodeIndex(1);
            }else{
                dtp.PushUl(1);
                dtp.PushUl(0);
                ZzExit();
            }
        }
        
        /*--------------------- $$JMP  ----------------------------------*/
        private void ZzJmpRelative(){    /* ;WORD $$JMP_RELATIVE */
                /* Mコード=0x002E */
            //SKIP_MCODE( READ_MCODE_SHORT );
            mp.MoveMcodeIndexByByte(mp.ReadMcodeShort());
        }
        private void ZzJmpIndexed(){     /* ;WORD $$JMP_INDEXED */
                /* Mコード=0x002F */
            //SET_MCODE_POINTER_BY_WORDNO( READ_MCODE );
            //#define  READ_MCODE	    (*McodePointer.w)
            //McodePointer.b = (UCHAR *)(WordOffsetTable[wordNo])
            mp.ResetMcodeIndex((uint)lp.GetSl(mp.ReadMcode()));   
        }
        private void ZzPointNull(){
                /* Mコード=0x0030 */
            //AccessPointer.w = NULL;
            ap.SetUs(0);
        }
        private void ZzPushNull(){   /* ;WORD  $$PUSH_NULL */
                                            /* ;ALIAS $$NULL      */
                /* Mコード=0x0031 */
                /* Mコード=0x0032 */
            //PUSH_S( NULL, 0 );
            //#define  PUSH_S(addr,count)   PUSH_A(addr),PUSH_C(count)
            dtp.PushUl(0);
            dtp.PushUl(0);
        }

        private void ZzPushPointer(){    /* ;WORD $$PUSH_POINTER */
                /* Mコード=0x0033 */
            //PUSH_Q( AccessPointer.u , 0 );
            dtp.PushUl(ap.GetCurrentDataIndex());
            dtp.PushUl(0);
        }
        private void ZzPushPointerPlusSize(){    /* ;WORD $$PUSH_POINTER+SIZE */
                /* Mコード=0x0034 */
            //PUSH_S( AccessPointer.u, FETCH_MCODE_LONG );
            // #define  PUSH_L(value)        *(--DstackPointer)=(LONG)(value)
            // #define  PUSH_U(value)        PUSH_L(value)
            // #define  PUSH_A(addr)         PUSH_U(addr)
            // #define  PUSH_C(count)        PUSH_U(count)
            // #define  PUSH_S(addr,count)   PUSH_A(addr),PUSH_C(count)
            dtp.PushUl(ap.GetCurrentDataIndex());
            dtp.PushUl(mp.GetUl());
        }
        private void AddressWoEru(){          /* ;WORD アドレスを得る */
                /* Mコード=0x0035 */
            //WRITE_C_OF_S( 0 );
            // #define  WRITE_F_OF_Q(value)  *DstackPointer=(LONG)(value)
            // #define  WRITE_C_OF_S(count)  WRITE_F_OF_Q(count)
            dtp.SetUl(0);
        }

        /*---------------------  大域変数のセットアップ  ------------------*/
        private void ZzPointQarray()
        {
            uint offset = mp.FetchMcodeInt();
            uint elements = mp.FetchMcodeInt();
            dtp.PopUl();
            uint index = dtp.PopUl();
            if (index == 0 || index > elements)
            {
                throw new IndexOutOfRangeException($"Array index {index} is outside 1..{elements}.");
            }
            ap.ResetDataIndex(checked(offset + (index - 1) * 8));
        }

        private void ZzPointVarAddr(){   /* ;WORD $$POINT_VARADDR */
                /* Mコード=0x0036 */
                //AccessPointer.b = DataBase + FETCH_MCODE_LONG;
                //#define  FETCH_MCODE_LONG   (*McodePointer.l++)
                ap.ResetDataIndex(mp.GetUl());
        }
        private void ZzPointCodeAddr(){  /* ;WORD $$POINT_CODEADDR */
                /* Mコード=0x0036 */
            //SET_APb_by_MBASE_PLUS_FETCH_MCODE_LONG;
            // #define  SET_APb_by_MBASE_PLUS_FETCH_MCODE_LONG AccessPointer.b=(UCHAR *)McodeBase+FETCH_MCODE_LONG
            ap.ResetDataIndex(mp.FetchMcodeInt());//byte配列なのでアドレス絶対値がない
        }
        /*---------------------  大域集合のセットアップ  ------------------*/
        private void ZzPushVarAddrPlusSize(){    /* ;WORD $$PUSH_VARADDR+SIZE */
                /* Mコード=0x0037 */
            //PUSH_A( DataBase + FETCH_MCODE_LONG );
            //PUSH_C( FETCH_MCODE_LONG );
            // #define  PUSH_L(value)        *(--DstackPointer)=(LONG)(value)
            // #define  PUSH_U(value)        PUSH_L(value)
            // #define  PUSH_A(addr)         PUSH_U(addr)
            // #define  PUSH_C(count)        PUSH_U(count)
            dtp.PushUl(DataAddressBit | mp.FetchMcodeInt());
            dtp.PushUl(mp.FetchMcodeInt());
        }
        private void ZzPushMcodeAddrPlusSize(){	/* ;WORD $$PUSH_MCODEADDR+SIZE */
                /* Mコード=0x0039 */
            // PUSH_A_by_MBASE_PLUS_FETCH_MCODE_LONG;
            // PUSH_C( FETCH_MCODE_LONG );
            //#define  PUSH_A_by_MBASE_PLUS_FETCH_MCODE_LONG  PUSH_A((UCHAR *)McodeBase+FETCH_MCODE_LONG)
            dtp.PushUl(mp.FetchMcodeInt());//byte配列なので絶対アドレスない
            dtp.PushUl(mp.FetchMcodeInt());
        }
        /*---------------------  局所変数のセットアップ  ------------------*/
        private void ZzPointLocalAddr0W(){ /* ;WORD $$POINT_LOCALADDR_0W */
                /* Mコード=0x003A */
            //AccessPointer.s = (SHORT *)RstackPointer;
            ap.PointStack(rtp, 0);
        }
        private void ZzPointLocalAddr1W(){ /* ;DUMMYWORD */
                /* Mコード=0x003B */
            //AccessPointer.s = (SHORT *)RstackPointer + 1;
            ap.PointStack(rtp, 2);
        }
        private void ZzPointLocalAddr2W(){ /* ;DUMMYWORD */
                /* Mコード=0x003C */
            //AccessPointer.s = (SHORT *)RstackPointer + 2;
            ap.PointStack(rtp, 4);
        }
        private void ZzPointLocalAddr3W(){ /* ;DUMMYWORD */
                /* Mコード=0x003D */
            //AccessPointer.s = (SHORT *)RstackPointer + 3;
            ap.PointStack(rtp, 6);
        }
        private void ZzPointLocalAddr4W(){ /* ;DUMMYWORD */
                /* Mコード=0x003E */
            //AccessPointer.s = (SHORT *)RstackPointer + 4;
            ap.PointStack(rtp, 8);
        }
        private void ZzPointLocalAddr5W(){ /* ;DUMMYWORD */
                /* Mコード=0x003F */
            //AccessPointer.s = (SHORT *)RstackPointer + 5;
            ap.PointStack(rtp, 10);
        }
        private void ZzPointLocalAddr6W(){ /* ;DUMMYWORD */
                /* Mコード=0x0040 */
            //AccessPointer.s = (SHORT *)RstackPointer + 6;
            ap.PointStack(rtp, 12);
        }
        private void ZzPointLocalAddr7W(){ /* ;DUMMYWORD */
                /* Mコード=0x0041 */
            //AccessPointer.s = (SHORT *)RstackPointer + 7;
            ap.PointStack(rtp, 14);
        }
        private void ZzPointLocalAddr(){  /* ;WORD $$POINT_LOCALADDR */
                /* Mコード=0x0042 */
            //AccessPointer.b = (UCHAR *)RstackPointer + FETCH_MCODE;
            ap.PointStack(rtp, mp.FetchMcode());
        }
        /*---------------------  局所集合のセットアップ  ------------------*/
        private void ZzPushLocalAddrPlusSize(){     /* ;WORD  $$PUSH_LOCALADDR+SIZE */
                                                    /* ;ALIAS $$READ_XVAR_LOCAL */
                /* Mコード=0x0043 */
                /* Mコード=0x0044 */
            // PUSH_A( (UCHAR *)RstackPointer + FETCH_MCODE );
            // PUSH_C( FETCH_MCODE );
           dtp.PushUl(mp.FetchMcode());//byte配列なので絶対アドレスない
           dtp.PushUl(mp.FetchMcode());

        }
        /*---------------------  変数の間接読み出し  ----------------------*/
        private void ZzPushBvar(){   /* ;WORD $$PUSH_BVAR */
                /* Mコード=0x0045 */
            // PUSH_L( *AccessPointer.b );
            // PUSH_F( 0 );
            dtp.PushUl(ap.GetUb());
            dtp.PushUl(0);  
        }
        private void ZzPushWvar(){   /* ;WORD $$PUSH_WVAR */
                /* Mコード=0x0046 */
            // PUSH_L( *AccessPointer.w );
            // PUSH_F( 0 );
            dtp.PushUl(ap.GetUs());
            dtp.PushUl(0);  
        }
        private void ZzPushDvar(){   /* ;WORD $$PUSH_DVAR */
                /* Mコード=0x0047 */
            // PUSH_L( *AccessPointer.l );
            // PUSH_F( 0 );
            dtp.PushUl(ap.GetUl());
            dtp.PushUl(0);  
        }
        private void ZzPushLvar(){   /* ;WORD $$PUSH_LVAR */
                /* Mコード=0x0048 */
            // PUSH_L( AccessPointer.l[0] );           /* push low  32bit  */
            // PUSH_L( AccessPointer.l[1] );           /* push high 32bit */
            ulong value = ap.GetUll();
            dtp.PushUl((uint)value);
            dtp.PushUl((uint)(value >> 32));
        }
        private void ZzPushQvar(){   /* ;WORD $$PUSH_QVAR */
                /* Mコード=0x0049 */
            // PUSH_L( AccessPointer.l[1] );           /* push high 32bit */
            // PUSH_L( AccessPointer.l[0] );           /* push low  32bit  */
            ulong value = ap.GetUll();
            dtp.PushUl((uint)(value >> 32));
            dtp.PushUl((uint)value);
        }
        private void ZzPushSvar(){   /* ;WORD $$PUSH_SVAR */
                /* Mコード=0x004A */
            // PUSH_A( AccessPointer.u[1] );           /* push address */
            // PUSH_C( AccessPointer.u[0] );           /* push length  */
            ulong value = ap.GetUll();
            dtp.PushUl((uint)(value >> 32));
            dtp.PushUl((uint)value);
        }
        private void ZzPushJvar(){
                /* Mコード=0x004B */
            // ULONG   length;
            // ULONG   margin;
            // length = *AccessPointer.u++;
            // margin = *AccessPointer.u++;
            // PUSH_A( AccessPointer.b + margin ); 	/* push address */
            // PUSH_C( length );                       /* push length  */
            throw new UnDefined(0);
        }

        /*---------------------  変数の直接読み出し  ----------------------*/
        private void ZzReadBvar(){
                /* Mコード=0x004C */
            // UCHAR  *varAddr;
            // varAddr = DataBase + FETCH_MCODE_LONG;
            // PUSH_L( *varAddr );
            // PUSH_F( 0 );
            ap.ResetDataIndex(mp.FetchMcodeInt());
            dtp.PushUl(ap.GetUb());
            dtp.PushUl(0);      
        }
        private void ZzReadWvar(){
                /* Mコード=0x004D */
            // USHORT *varAddr;
            // varAddr = (USHORT *)(DataBase + FETCH_MCODE_LONG);
            // PUSH_L( *varAddr );
            // PUSH_F( 0 );
            ap.ResetDataIndex(mp.FetchMcodeInt());
            dtp.PushUl(ap.GetUs());
            dtp.PushUl(0);       
        }
        private void ZzReadDvar(){
                /* Mコード=0x004E */
            // LONG   *varAddr;
            // varAddr = (LONG *)(DataBase + FETCH_MCODE_LONG);
            // PUSH_L( *varAddr );
            // PUSH_F( 0 );
            ap.ResetDataIndex(mp.FetchMcodeInt());
            dtp.PushUl(ap.GetUl());
            dtp.PushUl(0);
        }
        private void ZzReadLvar(){
                /* Mコード=0x004F */
            // ULONG   *varAddr;
            // varAddr = (LONG *)(DataBase + FETCH_MCODE_LONG);
            // PUSH_L( varAddr[0] );                   /* push low  double */
            // PUSH_L( varAddr[1] );                   /* push high double */
            ap.ResetDataIndex(mp.FetchMcodeInt());
            ulong value = ap.GetUll();
            dtp.PushUl((uint)value);
            dtp.PushUl((uint)(value >> 32));
        }
        private void ZzReadQvar(){
                /* Mコード=0x0050 */
            // ULONG   *varAddr;
            // varAddr = (LONG *)(DataBase + FETCH_MCODE_LONG);
            // PUSH_L( varAddr[1] );                   /* push high double */
            // PUSH_L( varAddr[0] );                   /* push low  double */
            ap.ResetDataIndex(mp.FetchMcodeInt());
            ulong value = ap.GetUll();
            dtp.PushUl((uint)(value >> 32));
            dtp.PushUl((uint)value);
        }
        private void ZzReadSvar(){
                /* Mコード=0x0051 */
            // ULONG  *varAddr;
            // varAddr = (ULONG *)(DataBase + FETCH_MCODE_LONG);
            // PUSH_A( varAddr[1] );                   /* push address */
            // PUSH_C( varAddr[0] );                   /* push length  */
            ap.ResetDataIndex(mp.FetchMcodeInt());
            ulong value = ap.GetUll();
            dtp.PushUl((uint)(value >> 32));
            dtp.PushUl((uint)value);
        }
        private void ZzReadJvar(){
                /* Mコード=0x0052 */
            // ULONG  *varAddr;
            // ULONG   length;
            // ULONG   margin;
            // varAddr = (ULONG *)(DataBase + FETCH_MCODE_LONG);
            // length = *varAddr++;
            // margin = *varAddr++;
            // PUSH_A( (UCHAR *)varAddr + margin );    /* push address */
            // PUSH_C( length );                       /* push length  */
            uint offset = mp.FetchMcodeInt();
            ap.ResetDataIndex(offset);
            ulong header = ap.GetUll();
            uint length = (uint)header;
            uint margin = (uint)(header >> 32);
            uint address = checked(offset + 8 + margin);
            if ((address & DataAddressBit) != 0)
                throw new ArgumentOutOfRangeException(nameof(address), "String data address exceeds the supported range.");
            dtp.PushUl(DataAddressBit | address);
            dtp.PushUl(length);
        }
        /*---------------------  局所変数の直接読み出し  ---------------*/
        private void ZzReadBvarLocal(){
                /* Mコード=0x0053 */
            // UCHAR  *varAddr;
            // varAddr = (UCHAR *)RstackPointer + FETCH_MCODE;
            // PUSH_L( *varAddr );
            // PUSH_F( 0 );
            //rtp.MoveStackPointer(mp.FetchMcode());//returnスタックのインデックスをMCode領域の値で移動
            //dtp.PushUl(rtp.PopUb());
            dtp.PushUl(rtp.ReadLocal(mp.FetchMcode(), 1));
            dtp.PushUl(0);
        }
        private void ZzReadWvarLocal(){
                /* Mコード=0x0054 */
            // USHORT *varAddr;
            // varAddr = (USHORT *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // PUSH_L( *varAddr );
            // PUSH_F( 0 );
            //rtp.MoveStackPointer(mp.FetchMcode());//returnスタックのインデックスをMCode領域の値で移動
            //dtp.PushUl(rtp.PopUs());
            dtp.PushUl(rtp.ReadLocal(mp.FetchMcode(), 2));
            dtp.PushUl(0);
        
        }
        private void ZzReadDvarLocal(){
                /* Mコード=0x0055 */
            // ULONG 	*varAddr;
            // varAddr = (ULONG *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // PUSH_L( *varAddr );
            // PUSH_F( 0 );            
            //rtp.MoveStackPointer(mp.FetchMcode());//returnスタックのインデックスをMCode領域の値で移動
            //int point =(int)rtp.GetUb() + mp.FetchMcode();//returnスタックポインタの指すバイト値とMCode領域の値を加算
            dtp.PushUl(rtp.ReadLocal(mp.FetchMcode(), 4));
            //dtp.PushUl(rtp.PopUl());
            dtp.PushUl(0);

        }
        private void ZzReadLvarLocal(){
                /* Mコード=0x0056 */
            // ULONG 	*varAddr;
            // varAddr = (ULONG *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // PUSH_L( varAddr[0] );                   /* push low  double */
            // PUSH_L( varAddr[1] );                   /* push high double */
            int offset = mp.FetchMcode();
            dtp.PushUl(rtp.ReadLocal(offset, 4));
            dtp.PushUl(rtp.ReadLocal(offset + 4, 4));
        }
        private void ZzReadQvarLocal(){
                /* Mコード=0x0057 */
            // ULONG 	*varAddr;
            // varAddr = (ULONG *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // PUSH_L( varAddr[1] );                   /* push high double */
            // PUSH_L( varAddr[0] );                   /* push low  double *
            int offset = mp.FetchMcode();
            dtp.PushUl(rtp.ReadLocal(offset + 4, 4));
            dtp.PushUl(rtp.ReadLocal(offset, 4));
        }
        private void ZzReadSvarLocal(){
                /* Mコード=0x0058 */
            // ULONG  	*varAddr;
            // varAddr = (ULONG *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // PUSH_A( varAddr[1] );                   /* push address */
            // PUSH_C( varAddr[0] );                   /* push length  */
            int offset = mp.FetchMcode();
            dtp.PushUl(rtp.ReadLocal(offset + 4, 4));
            dtp.PushUl(rtp.ReadLocal(offset, 4));
        }
        private void ZzReadJvarLocal(){
                /* Mコード=0x0059 */
            // ULONG  *varAddr;
            // ULONG   length;
            // ULONG   margin;
            // varAddr = (ULONG *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // length = *varAddr++;
            // margin = *varAddr++;
            // PUSH_A( (UCHAR *)varAddr + margin );            /* push address */
            // PUSH_C( length );                               /* push length  */
            throw new UnDefined(0);

        }
        /*---------------------  変数の間接書き込み  ------------------*/
        private void ZzPopBvar(){
                /* Mコード=0x005A */
            //*AccessPointer.b = POP_B_OF_Q;
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUb(dtp.PopUb());       
        }
        private void ZzPopWvar(){
                /* Mコード=0x005B */
            //*AccessPointer.w = POP_W_OF_Q;
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUs(dtp.PopUs()); 
        }
        private void ZzPopDvar(){
                /* Mコード=0x005C */
            //*AccessPointer.l = POP_L_OF_Q
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUl(dtp.PopUl()); 
        }
        private void ZzPopLvar(){
                /* Mコード=0x005D */
            // AccessPointer.l[1] = POP_L;	/* store high 32bit */
            // AccessPointer.l[0] = POP_L;	/* store low  32bit */
            uint var1=dtp.PopUl();//varAddr[1]用をデータスタックよりPOPしておく
            ap.SetUl2var(dtp.PopUl(),var1);
        }
        private void ZzPopQvar(){
                /* Mコード=0x005E */
            // AccessPointer.l[0] = POP_L;	/* store low  32bit */
            // AccessPointer.l[1] = POP_L;	/* store high 32bit */
            ap.SetUl2var(dtp.PopUl(),dtp.PopUl());       
        }
        private void ZzPopSvar(){
                /* Mコード=0x005F */
            // AccessPointer.u[0] = POP_C;
            // AccessPointer.u[1] = (ULONG)POP_A;
            ap.SetUl2var(dtp.PopUl(),dtp.PopUl());   
        }
        private void ZzPopMemory(){
                /* Mコード=0x0060 */
            throw new UnDefined(0);
        }
        private void ZzPopMemoryWithResult(){
                /* Mコード=0x0061 */
                /* Mコード=0x0062 */
            throw new UnDefined(0);
        }

        /*---------------------  変数の直接書き込み  --------------------*/
        private void ZzWriteBvar(){
                /* Mコード=0x0063 */
            //UCHAR  *varAddr;
            //varAddr = DataBase + FETCH_MCODE_LONG;
            //*varAddr = POP_B_OF_Q;

            // #define  DROP_L               DstackPointer++
            // #define  DROP_F               DROP_L
            // #define  POP_L                (*DstackPointer++)
            // #define  POP_F                POP_L
            // #define  POP_B_OF_Q           (DROP_F,(UCHAR)POP_L)
            ap.ResetDataIndex(mp.FetchMcodeInt());
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUb((byte)dtp.PopUl());
        }
        private void ZzWriteWvar(){
                /* Mコード=0x0064 */
            // USHORT *varAddr;
            //varAddr = (USHORT *)(DataBase + FETCH_MCODE_LONG);
            //*varAddr = POP_W_OF_Q;

            // #define  DROP_L               DstackPointer++
            // #define  DROP_F               DROP_L
            // #define  POP_L                (*DstackPointer++)
            // #define  POP_F                POP_L
            // #define  POP_W_OF_Q           (DROP_F,(USHORT)POP_L)
            ap.ResetDataIndex(mp.FetchMcodeInt());
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUs((ushort)dtp.PopUl());
        }
        private void ZzWriteDvar(){   /* ;WORD $$WRITE_DVAR */
                /* Mコード=0x0065 */
            // ULONG  *varAddr;
            // varAddr = (ULONG *)(DataBase + FETCH_MCODE_LONG);
            // *varAddr = POP_L_OF_Q;

            // #define  POP_L_OF_Q           (DROP_F,POP_L)
            ap.ResetDataIndex(mp.FetchMcodeInt());
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUl(dtp.PopUl());       
        }
        private void ZzWriteLvar(){
                /* Mコード=0x0066 */
            //ULONG  *varAddr;
            //varAddr = (ULONG *)(DataBase + FETCH_MCODE_LONG);
            //varAddr[1] = POP_L;
            //varAddr[0] = POP_L;

            //#define  POP_L                (*DstackPointer++)
            ap.ResetDataIndex(mp.FetchMcodeInt());
            uint var0=dtp.PopUl();//varAddr[0]用をデータスタックよりPOPしておく
            ap.SetUl4Ms(dtp.PopUl(),var0);
        }
        private void ZzWriteQvar(){
                /* Mコード=0x0067 */
            //ULONG  *varAddr;
            //varAddr = (ULONG *)(DataBase + FETCH_MCODE_LONG);
            //varAddr[0] = POP_L;
            //varAddr[1] = POP_L;
            
            //#define  POP_L                (*DstackPointer++)
            ap.ResetDataIndex(mp.FetchMcodeInt());
            ap.SetUl4Ms(dtp.PopUl(),dtp.PopUl());
        }
        private void ZzWriteSvar(){
                /* Mコード=0x0068 */
            //ULONG  *varAddr;
            //varAddr = (ULONG *)(DataBase + FETCH_MCODE_LONG);
            //varAddr[0] = POP_C;
            //varAddr[1] = (ULONG)POP_A;
            
            //#define  POP_L                (*DstackPointer++)
            //#define  POP_U                ((ULONG)(*DstackPointer++))
            //#define  POP_C                POP_U
            //#define  POP_A                ((UCHAR *)(*DstackPointer++))
            ap.ResetDataIndex(mp.FetchMcodeInt());
            ap.SetUl4Ms(dtp.PopUl(),dtp.PopUl());   
        }

        /*---------------------  局所変数の直接書き込み  --------------------*/
        private void ZzWriteBvarLocal(){ /* ;WORD $$WRITE_BVAR_LOCAL */
                /* Mコード=0x0069 */
            // UCHAR  *varAddr;   
            // varAddr = (UCHAR *)RstackPointer + FETCH_MCODE;
            // *varAddr = POP_B_OF_Q;
            
            // #define  DROP_L               DstackPointer++
            // #define  DROP_F               DROP_L
            // #define  POP_L                (*DstackPointer++)
            // #define  POP_F                POP_L
            // #define  POP_B_OF_Q           (DROP_F,(UCHAR)POP_L)
            //rtp.MoveStackPointer(mp.FetchMcode());//returnスタックのインデックスをMCode領域の値で移動
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            //rtp.PushUb((byte)dtp.PopUl());
            rtp.WriteLocal(mp.FetchMcode(), dtp.PopUl(), 1);
        }
        private void ZzWriteWvarLocal(){  /* ;WORD $$WRITE_WVAR_LOCAL */
                /* Mコード=0x006A */
            // USHORT *varAddr;
            // varAddr = (USHORT *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // *varAddr = POP_W_OF_Q;

            // #define  DROP_L               DstackPointer++
            // #define  DROP_F               DROP_L
            // #define  POP_L                (*DstackPointer++)
            // #define  POP_F                POP_L
            // #define  POP_W_OF_Q           (DROP_F,(USHORT)POP_L)
            //rtp.MoveStackPointer(mp.FetchMcode());//returnスタックのインデックスをMCode領域の値で移動
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            //rtp.PushUs((ushort)dtp.PopUl());
            rtp.WriteLocal(mp.FetchMcode(), dtp.PopUl(), 2);
        }
        private void ZzWriteDvarLocal(){ /* ;WORD $$WRITE_DVAR_LOCAL */
                /* Mコード=0x006B */
            // LONG  	*varAddr;
            // varAddr = (LONG *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // *varAddr = POP_L_OF_Q;

            // #define  POP_L_OF_Q           (DROP_F,POP_L)
            //rtp.MoveStackPointer(mp.FetchMcode());//returnスタックのインデックスをMCode領域の値で移動
            //int point =(int)rtp.GetUb() + mp.FetchMcode();//returnスタックポインタの指すバイト値とMCode領域の値を加算
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            rtp.WriteLocal(mp.FetchMcode(), dtp.PopUl(), 4);
            //rtp.MoveStackPointer(mp.FetchMcode());//returnスタックのインデックスをMCode領域の値で移動
            //dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            //rtp.PushUl(dtp.PopUl());


        }
        private void ZzWriteLvarLocal(){ /* ;WORD $$WRITE_LVAR_LOCAL */
                /* Mコード=0x006C */
            // ULONG  	*varAddr;
            // varAddr = (ULONG *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // varAddr[1] = POP_L;
            // varAddr[0] = POP_L;
            int offset = mp.FetchMcode();
            uint high = dtp.PopUl();
            rtp.WriteLocal(offset, dtp.PopUl(), 4);
            rtp.WriteLocal(offset + 4, high, 4);
        }
        private void ZzWriteQvarLocal(){ /* ;WORD $$WRITE_QVAR_LOCAL */
                /* Mコード=0x006D */
            // ULONG  	*varAddr;
            // varAddr = (ULONG *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // varAddr[0] = POP_L;
            // varAddr[1] = POP_L;
            int offset = mp.FetchMcode();
            rtp.WriteLocal(offset, dtp.PopUl(), 4);
            rtp.WriteLocal(offset + 4, dtp.PopUl(), 4);
        }
        private void ZzWriteSvarLocal(){ /* ;WORD $$WRITE_SVAR_LOCAL */
                /* Mコード=0x006E */
            // ULONG  *varAddr;
            // varAddr = (ULONG *)((UCHAR *)RstackPointer + FETCH_MCODE);
            // varAddr[0] = POP_C;
            // varAddr[1] = (ULONG)POP_A;

            // #define  POP_A                ((UCHAR *)(*DstackPointer++))
            // #define  POP_C                POP_U
            int offset = mp.FetchMcode();
            rtp.WriteLocal(offset, dtp.PopUl(), 4);
            rtp.WriteLocal(offset + 4, dtp.PopUl(), 4);
        }

        /*---------------------  変数のクリア  --------------------*/
        private void ZzClearBvar(){ /* ;WORD $$CLR_BVAR */
                /* Mコード=0x006F */
            // *AccessPointer.b = 0;
            ap.SetUb(0);//b=unsigned byte
        }
        private void ZzClearWvar(){  /* ;WORD $$CLR_WVAR */
                /* Mコード=0x0070 */
            //*AccessPointer.w = 0;
            ap.SetUs(0);//w=unsigned short
        }
        private void ZzClearDvar(){  /* ;WORD $$CLR_DVAR */
            /* Mコード=0x0071 */
            //*AccessPointer.l = 0;
            ap.SetSl(0);//l=signed int32
        }
        private void ZzClearLvar(){  /* ;WORD $$CLR_LVAR */
                /* Mコード=0x0072 */
            //*AccessPointer.ll = 0;	/*注：64bit*/;
            ap.SetUll(0);//ll=unsigned int64
        }
        private void ZzClearFvar(){ /* ;WORD $$CLR_FVAR */
                /* Mコード=0x0073 */
            //*AccessPointer.f = 0.0f;	/*注：64bit*/;
            throw new UnDefined(0);
        }
        private void ZzClearSvar(){  /* ;WORD $$CLR_SVAR */
                /* Mコード=0x0074 */
            //AccessPointer.u[0] = 0;
            //AccessPointer.u[1] = 0;
            ap.SetUl4Ms(0,0);//ul=unsigned int32 Ms=mind string
        }
        private void ZzClearMemory(){
                /* Mコード=0x0075 */
                /* Mコード=0x0076 */
            // void   *address;
            // size_t  length;
            // length  = (size_t)POP_C;
            // address = (void *)POP_A;
            // memset( address, 0, length );
            uint length = dtp.PopUl();
            uint address = dtp.PopUl();
            if ((address & DataAddressBit) != 0)
                ap.ClearBytes(address & ~DataAddressBit, length);
            else
                mp.ClearBytes(address, length);
        }

        /*--------------------- 変数を一つ増加  -------------------------*/
        /*** 注意:以下の4単語は定義順序を変えてはならない **/
        private void ZzIncBvar(){/* ;WORD $$INC_BVAR */
                /* Mコード=0x0077 */
            //(*AccessPointer.b)++
            ap.SetUb((byte)(ap.GetUb() + 1));
        }
        private void ZzIncWvar(){/* ;WORD $$INC_WVAR */
                /* Mコード=0x0078 */
            //(*AccessPointer.w)++;
            ap.SetUs((ushort)(ap.GetUs() + 1));
        }
        private void ZzIncDvar(){/* ;WORD $$INC_DVAR */
                /* Mコード=0x0079 */
            //(*AccessPointer.l)++;
            ap.SetUl(ap.GetUl() + 1);
        }
        private void ZzIncLvar(){/* ;WORD $$INC_LVAR */
                /* Mコード=0x007A */
            //(*AccessPointer.ll)++;
            ap.SetUll(ap.GetUll() + 1);
        }
        /*--------------------- 変数を一つ減少  -------------------------*/
        /*** 注意:以下の4単語は定義順序を変えてはならない **/
        private void ZzDecBvar(){   /* ;WORD $$DEC_BVAR */
                /* Mコード=0x007B */
            //(*AccessPointer.b)--;
            ap.SetUb((byte)(ap.GetUb() - 1));
        }
        private void ZzDecWvar(){   /* ;WORD $$DEC_WVAR */
                /* Mコード=0x007C */
            //(*AccessPointer.w)--;
            ap.SetUs((ushort)(ap.GetUs() - 1));
        }
        private void ZzDecDvar(){   /* ;WORD $$DEC_DVAR */
                /* Mコード=0x007D */
            //(*AccessPointer.l)--;
            ap.SetUl(ap.GetUl() - 1);
        }
        private void ZzDecLvar(){   /* ;WORD $$DEC_LVAR */
                /* Mコード=0x007E */
            //(*AccessPointer.ll)--;
            ap.SetUll(ap.GetUll() - 1);
        }

        /*--------------------- 変数をセット  ---------------------------*/
        private void ZzSetBvar(){    /* ;WORD $$SET_BVAR */
                /* Mコード=0x007F */
            //*AccessPointer.b = 1;
            ap.SetUb(1);
        }
        private void ZzSetWvar(){    /* ;WORD $$SET_WVAR */
                /* Mコード=0x0080 */
            //*AccessPointer.w = 1;
            //USHORT* w;      /* for unsigned word access */
            ap.SetUs(1);
        }
        private void ZzSetDvar(){    /* ;WORD $$SET_DVAR */
                /* Mコード=0x0081 */
            //*AccessPointer.l = 1;
            ap.SetUl(1);
        }
        private void ZzSetLvar(){   /* ;WORD $$SET_LVAR */
                /* Mコード=0x0082 */
            //*AccessPointer.ll = 1;
            ap.SetUll(1);
        }
        private void c_test_push_quad(){	    /* ;WORD c_test_push_quad */
                /* Mコード=0x0083 */
            throw new UnDefined(0);
        }
        private void c_test_push_float(){    /* ;WORD c_test_push_float */
                /* Mコード=0x0084 */
            throw new UnDefined(0);
        }
        /*--------------------- 変数を増加  -----------------------------*/
        private void ZzAddBvar(){    /* ;WORD $$ADD_BVAR */
                /* Mコード=0x0085 */
            //*AccessPointer.b += POP_B_OF_Q;
            // #define  POP_B_OF_Q           (DROP_F,(UCHAR)POP_L)
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUb((byte)(ap.GetUb() + dtp.PopUl()));
        }
        private void ZzAddWvar(){    /* ;WORD $$ADD_WVAR */
                /* Mコード=0x0086 */
            //*AccessPointer.w += POP_W_OF_Q;
            // #define  POP_W_OF_Q           (DROP_F,(USHORT)POP_L)
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUs((ushort)(ap.GetUs() + dtp.PopUl()));
        }
        private void ZzAddDvar(){    /* ;WORD $$ADD_DVAR */
                /* Mコード=0x0087 */
            //*AccessPointer.l += POP_L_OF_Q;
            // #define  POP_L_OF_Q           (DROP_F,POP_L)
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUl(ap.GetUl() + dtp.PopUl());
        }
        private void ZzAddLvar(){    /* ;WORD $$ADD_LVAR */
                /* Mコード=0x0088 */
            // word64 high32;
            // ULONG  low32;
            // word64 value64;
            // typedef unsigned __int64       word64;
            // high32 = POP_L;
            // low32  = POP_L;
            // value64 = (high32<<32 | low32);
            /*	printf("$$ADD_LVARが呼ばれた AccessPointer=%08lx value64=%lld\n", AccessPointer, value64);*/
            // *AccessPointer.ll += value64;
            ulong high32 = dtp.PopUl();
            uint low32   = dtp.PopUl();
            ulong value64= high32<<32 | low32;
            ap.SetUll(ap.GetUll() + value64);
        }
        private void ZzAddFvar(){    /* ;WORD $$ADD_FVAR */
                /* Mコード=0x0089 */
            // double	fval;
            // fval = pop_float();
            // *AccessPointer.f += fval;
            throw new UnDefined(0);
        }
        /*--------------------- 変数を減少  -----------------------------*/
        private void ZzSubBvar(){   /* ;WORD $$SUB_BVAR */
                /* Mコード=0x008A */
            // *AccessPointer.b -= POP_B_OF_Q;
            // #define  POP_B_OF_Q           (DROP_F,(UCHAR)POP_L)
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUb((byte)(ap.GetUb() - dtp.PopUl()));
        }
        private void ZzSubWvar(){   /* ;WORD $$SUB_WVAR */
                /* Mコード=0x008B */
            // *AccessPointer.w -= POP_W_OF_Q;
            // #define  POP_W_OF_Q           (DROP_F,(USHORT)POP_L)
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUs((ushort)(ap.GetUs() - dtp.PopUl()));
        }
        private void ZzSubDvar(){   /* ;WORD $$SUB_DVAR */
                /* Mコード=0x008C */
            // *AccessPointer.l -= POP_L_OF_Q;
            // #define  POP_L_OF_Q           (DROP_F,POP_L)
            dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
            ap.SetUl(ap.GetUl() - dtp.PopUl());
        }
        private void ZzSubLvar(){   /* ;WORD $$SUB_LVAR */
                /* Mコード=0x008D */
            // word64 high32;
            // ULONG	      low32;
            // word64 value64;
            // high32 = POP_L;
            // low32  = POP_L;
            // value64 = (high32<<32 | low32);
            // *AccessPointer.ll -= value64;
            ulong high32 = dtp.PopUl();
            uint low32   = dtp.PopUl();
            ulong value64= high32<<32 | low32;
            ap.SetUll(ap.GetUll() - value64);
        }
        private void ZzSubFvar(){   /* ;WORD $$SUB_FVAR */
                /* Mコード=0x008E */
            // double	fval;
            // fval = pop_float();
            // *AccessPointer.f -= fval;
            throw new UnDefined(0);        
        }
        
        /*--------------------- バイトロード～ダブルロード  ----------------*/
        private void ByteLoad(){     /* ;WORD バイトロード .N */
                /* Mコード=0x008F */
            //WRITE_L_OF_Q( *READ_A_OF_S );	/*注:一旦変数に受けていたが廃止*/
            // #define  WRITE_L_OF_Q(value)  *(DstackPointer+1)=(LONG)(value)
            // #define  READ_A_OF_S          ((UCHAR *)DstackPointer[1])
            dtp.SetUlOffset(dtp.GetUlOffset(+1),+1);//offset-1->+1
        }
        private void WordLoad(){     /* ;WORD ワードロード .N */
                /* Mコード=0x0090 */
            // USHORT  *varAddr;
            // varAddr = (USHORT *)READ_A_OF_S;
            // WRITE_L_OF_Q( *varAddr );
            throw new UnDefined(0);
        }
        private void DoubleLoad(){    /* ;WORD ダブルロード .N */
                /* Mコード=0x0091 */
            // ULONG  *varAddr;
            // varAddr = (ULONG *)READ_A_OF_S;
            // WRITE_L_OF_Q( *varAddr );
            throw new UnDefined(0);
        }
        /*--------------------- バイトストア～ダブルストア  ----------------*/
        private void ByteStore(){     /* ;WORD バイトストア NN */
                /* Mコード=0x0092 */
            // UCHAR  *varAddr;
            // varAddr = (UCHAR *)POP_U_OF_Q;
            // *varAddr = POP_B_OF_Q;
            throw new UnDefined(0);
        }
        private void WordStore(){     /* ;WORD ワードストア NN */
                /* Mコード=0x0093 */
            // USHORT  *varAddr;
            // varAddr = (USHORT *)POP_U_OF_Q;
            // *varAddr = POP_W_OF_Q;
            throw new UnDefined(0);
        }
        private void DoubleStore(){  /* ;WORD ダブルストア NN */
                /* Mコード=0x0094 */
            // ULONG  	*varAddr;
            // varAddr = (ULONG *)POP_L_OF_Q;
            // *varAddr = POP_L_OF_Q;
            throw new UnDefined(0);
        }

        /*--------------------- 数値定数  -------------------------------*/
        private void Zz0(){  /* ;WORD $$0 */
                /* Mコード=0x0095 */
            //PUSH_Q( 0, 0 );
            // #define  PUSH_L(value)        *(--DstackPointer)=(LONG)(value)
            // #define  PUSH_F(value)        PUSH_L(value)
            // #define  PUSH_Q(i,f)          PUSH_L(i),PUSH_F(f)
            dtp.PushUl(0);
            dtp.PushUl(0);
        }
        private void Zz1(){  /* ;DUMMYWORD */
                /* Mコード=0x0096 */
            //PUSH_Q( 1, 0 );
            dtp.PushUl(1);
            dtp.PushUl(0);
        }
        private void Zz2(){  /* ;DUMMYWORD */
                /* Mコード=0x0097 */
            //PUSH_Q( 2, 0 );
            dtp.PushUl(2);
            dtp.PushUl(0);
        }
        private void Zz3(){  /* ;DUMMYWORD */
                /* Mコード=0x0098 */
            //PUSH_Q( 3, 0 );
            dtp.PushUl(3);
            dtp.PushUl(0);
        }
        private void Zz4(){  /* ;DUMMYWORD */
                /* Mコード=0x0099 */
            //PUSH_Q( 4, 0 );
            dtp.PushUl(4);
            dtp.PushUl(0);
        }
        private void Zz5(){  /* ;DUMMYWORD */
                /* Mコード=0x009A */
            //PUSH_Q( 5, 0 );
            dtp.PushUl(5);
            dtp.PushUl(0);
        }
        private void Zz6(){  /* ;DUMMYWORD */
                /* Mコード=0x009B */
            //PUSH_Q(6, 0 );
            dtp.PushUl(6);
            dtp.PushUl(0);
        }
        private void Zz7(){  /* ;DUMMYWORD */
                /* Mコード=0x009C */
            //PUSH_Q( 7, 0 );
            dtp.PushUl(7);
            dtp.PushUl(0);
        }
        private void Zz8(){  /* ;DUMMYWORD */
                /* Mコード=0x009D */
            //PUSH_Q( 8, 0 );
            dtp.PushUl(8);
            dtp.PushUl(0);
        }
        private void Zz9(){  /* ;DUMMYWORD */
                /* Mコード=0x009E */
            //PUSH_Q( 9, 0 );
            dtp.PushUl(9);
            dtp.PushUl(0);
        }
        private void Zz10(){  /* ;DUMMYWORD */
                /* Mコード=0x009F */
            //PUSH_Q( 10, 0 );
            dtp.PushUl(10);
            dtp.PushUl(0);
        } 
        private void ZzWord(){   /* ;WORD $$WORD */
                /* Mコード=0x00A0 */
            // PUSH_U( FETCH_MCODE );                  /* 注:ULONGに拡張 */
            // PUSH_F( 0 );    
            dtp.PushUl(mp.FetchMcode());
            dtp.PushUl(0);
        }
        private void ZzMword(){   /* ;WORD $$MWORD */
                /* Mコード=0x00A1 */
            // PUSH_U( 0xffff0000L | FETCH_MCODE );    /* 注:ULONGに拡張 */
            // PUSH_F( 0 );   
            dtp.PushUl( 0xffff0000 | mp.FetchMcode());
            dtp.PushUl(0);
        }
        private void ZzDouble(){   /* ;WORD $$DOUBLE */
                /* Mコード=0x00A2 */
            // PUSH_U( FETCH_MCODE_LONG );
            // PUSH_F( 0 );
            dtp.PushUl(mp.GetUl());
            dtp.PushUl(0);
        }
        private void ZzQuad(){   /* ;WORD $$QUAD */
                /* Mコード=0x00A3 */
            // ULONG fraction = FETCH_MCODE_LONG;
            // ULONG integer  = FETCH_MCODE_LONG;
            // PUSH_U( integer );
            // PUSH_F( fraction );
            uint fraction = mp.GetUl();
            uint integer = mp.GetUl();
            dtp.PushUl(integer);
            dtp.PushUl(fraction);    
        }
        
        /*--------------------- 文字列定数  ----------------------------*/
        private void ZzSliteralOdd(){       /* ;WORD $$SLITERAL_ODD */
                /* Mコード=0x00A4 */
            // tMcode  length = FETCH_MCODE;
            // PUSH_A( McodePointer.b );                       /* push address */
            // PUSH_C( length );                               /* push length  */
            // SKIP_MCODE( length + 1 );
            ushort length=mp.FetchMcode();
            dtp.PushUl(mp.GetMcodeIndex());
            dtp.PushUl(length);
            mp.MoveMcodeIndexByByte(length + 1);
        }
        private void ZzSliteralEven(){      /* ;WORD $$SLITERAL_EVEN */
                /* Mコード=0x00A5 */
            // tMcode length = FETCH_MCODE;
            // PUSH_A( McodePointer.b );                       /* push address */
            // PUSH_C( length );                               /* push length  */
            // SKIP_MCODE( length );
            ushort length=mp.FetchMcode();
            dtp.PushUl(mp.GetMcodeIndex());
            dtp.PushUl(length);
            mp.MoveMcodeIndexByByte(length);
        }

        private void ZzClearXvar(){		/* ;WORD $$CLR_XVAR */
                /* Mコード=0x016E */
            /* ([AccessPointer(単純文字列実体)] → .) */
            //size_t varLength = (size_t)FETCH_MCODE_ULONG;
            /* クリアする */
            //memset( (void *)AccessPointer.b, SPACE, varLength );
            byte SPACE =32;
            uint varLength = mp.GetUl();
            ap.SetUb4Msi(SPACE,varLength);
        }

    }
}