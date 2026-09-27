namespace Mind8Kernel
{
    public partial class Dispatcher
    {
		/*--------------------- 数値計算  -------------------------------*/
		private void  HitotuKuwae()		/* ;WORD 一つ加え .N */
		{
				/* Mコード=0x0101 */
			//L_OF_Q += 1L;
			//#define  L_OF_Q		      READ_L_OF_Q
			//#define  READ_L_OF_Q          (DstackPointer[1])
			dtp.SetUlOffset(dtp.GetUlOffset(1)+1,1);
		}

		private void   FutatuKuwae()		/* ;WORD 二つ加え */
		{
				/* Mコード=0x0102 */
			//L_OF_Q += 2L;
			dtp.SetUlOffset(dtp.GetUlOffset(1)+2,1);
		}

		private void HitotuHiki()		/* ;WORD 一つ引き .N */
		{
				/* Mコード=0x0103 */
			//L_OF_Q -= 1L;
			dtp.SetUlOffset(dtp.GetUlOffset(1)-1,1);
		}

		private void FutatuHiki()		/* ;WORD 二つ引き .N */
		{
				/* Mコード=0x0104 */
			//L_OF_Q -= 2L;
			//#define  L_OF_Q		      READ_L_OF_Q
			//#define  READ_L_OF_Q          (DstackPointer[1])
			dtp.SetUlOffset(dtp.GetUlOffset(1)-2,1);
		}

		/* [n(16)を　加え]のコンパイリングワード */
		private void   ZzAddWimm()         	/* ;WORD $$ADD_WIMM */
		{
				/* Mコード=0x0105 */
			// L_OF_Q += FETCH_MCODE;
			dtp.SetUl(dtp.GetUlOffset(1)+mp.FetchMcode());
		}

		/* [n(32)を　加え]のコンパイリングワード */
		private void  ZzAddDimm()         	/* ;WORD $$ADD_DIMM */
		{
				/* Mコード=0x0106 */
			// L_OF_Q += FETCH_MCODE_LONG;
			dtp.SetUl(dtp.GetUlOffset(1)+mp.FetchMcodeInt());
		}

		/* [加え]のコンパイリングワード */
		private void	Kuwae()		/* ;WORD 加え       ※属性はgensymにて※　*/
		{
				/* Mコード=0x0107 */
				// LONG  data2;
				// data2  = POP_L_OF_Q;
				// L_OF_Q += data2;
				// #define  POP_L_OF_Q           (DROP_F,POP_L)
				//#define  L_OF_Q		      READ_L_OF_Q
				//#define  READ_L_OF_Q          (DstackPointer[1])
	        	dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
				int data2=(int)dtp.PopUl();
				dtp.SetUlOffset((uint)(dtp.GetUlOffset(1)+data2),1);

		}

		/* [n(16)を　引く]のコンパイリングワード */
		private void   ZzSubWimm()         	/* ;WORD $$SUB_WIMM */
		{
				/* Mコード=0x0108 */
			//L_OF_Q -= FETCH_MCODE;
			dtp.SetUl(dtp.GetUlOffset(1)-mp.FetchMcode());
		}
		/* [n(32)を　引く]のコンパイリングワード */
		private void   ZzSubDimm()         	/* ;WORD $$SUB_DIMM */
		{
				/* Mコード=0x0109 */
			// L_OF_Q -= FETCH_MCODE_LONG;
			dtp.SetUl(dtp.GetUlOffset(1)-mp.FetchMcodeInt());

		}
		/* [引く]のコンパイリングワード */
		private void Hiku()		/* ;WORD 引く       ※属性はgensymにて※　*/
		{
					/* Mコード=0x010A */
			// LONG  data2;
			// data2  = POP_L_OF_Q;
			// L_OF_Q -= data2;
			dtp.PopUl();//≒DROP Dataスタックから取り出して捨てる
			int data2=(int)dtp.PopUl();
			dtp.SetUl((uint)(dtp.GetUlOffset(1)-data2));
		}



		private void Sute()		/* ;WORD 捨て */
		{
					/* Mコード=0x0122 */
			//	DROP_Q;
			//#define  DROP_Q               DstackPointer+=2
			dtp.MoveStackPointer(2);
		}

		private void NibannmeWoSute()	/* ;WORD 二番目を捨て */
		{
					/* Mコード=0x0123 */
			/* (data1, data2 → data2) */
			/*  ↑これを捨てる         */
			// 	ULONG  fraction2;
			// 	ULONG  integer2;
			// fraction2 = POP_F;
			// integer2  = POP_U;
			// WRITE_Q( integer2 , fraction2 );
			// #define  WRITE_L_OF_Q(value)  *(DstackPointer+1)=(LONG)(value)
			// #define  WRITE_F_OF_Q(value)  *DstackPointer=(LONG)(value)
			// #define  WRITE_Q(i,f)         WRITE_L_OF_Q(i),WRITE_F_OF_Q(f)
			uint fraction2=dtp.PopUl();
			uint int2 = dtp.PopUl();
			dtp.SetSl2var((int)fraction2,(int)int2);

		}

		private void FutatuSute()		/* ;WORD 二つ捨て */
		{
					/* Mコード=0x0124 */
				// DROP_Q;
				// DROP_Q;
				//#define  DROP_Q               DstackPointer+=2
				dtp.MoveStackPointer(4);
		}
    }
}