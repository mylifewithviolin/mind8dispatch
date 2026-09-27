namespace Mind8Kernel
{
    public partial class Dispatcher
    {
        /*--------------------- 漢字コードの検査  (Shift-JIS版) --------*/
        private static bool IsThisByteKanji_SJIS(byte c) {
            return ((c)>=0x81 && (c)<= 0x9f) || (((c)>=0xe0) ? true : false);
        }
        private static bool IsThisByteKanji(byte c) {
            return IsThisByteKanji_SJIS(c);
        }	
        /*--------------------- isLastCharKanji()  (Shift-JIS版)-------*/
        private	uint  IsLastCharKanji(			/* 戻値:0or1 */
                // UCHAR	*s,		/* 引数1:NEXTアドレス */
                // size_t	length		/* 引数2:長さ         */
                uint indexNext,
                uint length
		   )
        {
            //     UCHAR   c;
            //     size_t	kanjiCounter;
             // s -= 2;					/* s=文字列の右端-1バイト目 */
            // kanjiCounter = 0;			/* 特殊カウンタをクリアする */           
            byte c;
            uint kanjiCounter=0;
            indexNext -= 2;	

            // while( length >= 2)
            // {
            //     c = *s--;			/* c=末尾から2バイト目   */
            //     if ( isThisByteKanji(c) )	/* それは漢字第一バイト? */
            //     {
            //         length--;		/* 文字列長さ1バイト縮小  */
            //         kanjiCounter++;		/* 特殊カウンタを一つ増加 */
            //     }
            //     else
            //     {
            //         break;
            //     }
            // }
            // return (kanjiCounter & 1);		/* 特殊カウンタが奇数なら */
            while(length >= 2){
                c=(byte)indexNext--;
                if(IsThisByteKanji(c) ){
                    length--;/* 文字列長さ1バイト縮小  */
                    kanjiCounter++;/* 特殊カウンタを一つ増加 */
                }else{
                    break;
                }
            }
            return kanjiCounter & 1;
        }	

        private uint LimitTail(				/* 戻値:削減バイト数 */
		uint		 length,	/* 引数1:文字列長さ */
		uint		 indexNext,	/* 引数2:文字列NEXTアドレス */
		uint		 limit		/* 引数3:制限桁数   */
	     )
        {
            uint deleted=0;
            uint	oneOrTwo;
            while( length > limit )/* 桁数 ＞ リミット */
            {
                oneOrTwo = IsLastCharKanji(indexNext, length ) + 1;
                length  -= oneOrTwo;
                deleted += oneOrTwo;
            }
            return deleted;
        }
        /*--------------------- 文字列実体変数への格納  ------------------*/   

        private void PopJvar_0(uint varIndex,uint varLength)
                    // tGeneralPointer  varAddr,	/* 第1引数:変数アドレス */
                    // size_t		 varLength	/* 第2引数:変数長さ     */
                    //  )
        {
                /* (strinfo → .) */

                // size_t		 length;
                // UCHAR		*string;
                // size_t		 deleted;

                /*-[変数]----------------------*/
                /*   +0(D): length             */
                /*   +4(D): margin             */
                /*   +6(B): "string"           */
                /*-----------------------------*/

            /* 文字列の取り出し */
            // POP_length_and_string;
            //#define POP_length_and_string  length=POP_C; string=POP_A                       
            //#define  POP_L                (*DstackPointer++)
            //#define  POP_U                ((ULONG)(*DstackPointer++))
            //#define  POP_C                POP_U
            //#define  POP_A                ((UCHAR *)(*DstackPointer++))
            uint length=dtp.PopUl();
            uint index=dtp.PopUl();
            // /* 格納元の文字列の桁数制限 */
            // if ( length > varLength )
            // {	/* 追加が変数より長い */
            // 	deleted = limitTail( length, (string+length), varLength );
            // 	length -= deleted;			/* 追加文字列を縮小 */
            // }
            if(length > varLength){
             	uint deleted = LimitTail( length, index+length, varLength );
             	length -= deleted;			/* 追加文字列を縮小 */
            }
            if(length>0){

            }
                /* ヘッダ部記入 */
            //     varAddr.u[0] = length;
            //     varAddr.u[1] = 0;

            //     /* 本体部転送 */
            // if ( length )
            //     {	/* ------------------------------------- */
            //     	/* 注:安全のため長さゼロの場合は呼ばない */
            // 	/* ------------------------------------- */
            // 	memmove( &(varAddr.u[2]), (void *)string, length );
            //     }
        }


        private void ZzPopJvar()           /* ;WORD $$POP_JVAR */
        {
                /* Mコード=0x0164 */
                /* (strinfo, [AccessPointer(文字列実体)] → .) */

            // 	size_t	varLength;

            //     /*-[Mcode生成]-----------------*/
            //     /*   +0(D): size(32)           */
            //     /*-----------------------------*/

            //     /*-[変数]----------------------*/
            //     /*   +0(D): length             */       /* ←[AccessPointer] */
            //     /*   +4(D): margin             */
            //     /*   +6(B): "string"           */
            //     /*-----------------------------*/

            // /* 変数長さを知る */
            // varLength = FETCH_MCODE_ULONG;
            // /* 文字列格納関数を呼ぶ */
            // 	popJvar_0( AccessPointer, varLength );
            uint varIndex=ap.GetCurrentDataIndex();
            uint varLength=mp.FetchMcodeInt();
            PopJvar_0(varIndex,varLength);
        }


        private void ZzWriteJvar()         /* ;WORD $$WRITE_JVAR */
        {
                /* Mコード=0x0165 */
                /* (strinfo → .) */

            //     /*-[Mcode生成]-----------------*/
            //     /*   +0(D): dataLocation       */
            //     /*   +4(D): size(32)           */
            //     /*-----------------------------*/

            // 		tGeneralPointer  varAddr;
            // 		size_t		 varLength;
            // /* ロケーションと変数長さを知る */
            // varAddr.b = DataBase + FETCH_MCODE_ULONG;
            // varLength = FETCH_MCODE_ULONG;
            // /* 文字列格納関数を呼ぶ */
            // 	popJvar_0( varAddr, varLength );
            uint varIndex=mp.FetchMcodeInt();
            uint varLength=mp.FetchMcodeInt();
            PopJvar_0(varIndex,varLength);
        }

        private void ZzWriteJvarLocal()    /* ;WORD $$WRITE_JVAR_LOCAL */
        {
                /* Mコード=0x0166 */
                /* (strinfo → .) */

            //     /*-[Mcode生成]-----------------*/
            //     /*   +0(W): offset             */
            //     /*   +2(W): size(16)           */
            //     /*-----------------------------*/

            // 		tGeneralPointer  varAddr;
            // 		size_t		 varLength;
            // varAddr.b = (UCHAR *)RstackPointer + FETCH_MCODE;
            // varLength = FETCH_MCODE;
            // /* 文字列格納関数を呼ぶ */
            // 	popJvar_0( varAddr, varLength );
            uint varIndex=mp.FetchMcode();
            uint varLength=mp.FetchMcode();
            PopJvar_0(varIndex,varLength);
        }

        private void MojiretuJittaiWoIre()	/* ;WORD 文字列実体を入れ .J XSW WRT HE */
        {
                /* Mコード=0x0167 */
            /* (strinfo, varinfo → .) */

            // 		tGeneralPointer  varAddr;
            // 		size_t	 	 varLength;
            // varLength = POP_C;
            // varAddr.b = POP_A;
            // /* 文字列格納関数を呼ぶ */
            // 	popJvar_0( varAddr, varLength );
            uint varLength=dtp.PopUl();
            uint varIndex=dtp.PopUl();
            PopJvar_0(varIndex,varLength);

        }

        /*----------- その他、文字列実体情報によるオペレーション  ---------*/
    }
}