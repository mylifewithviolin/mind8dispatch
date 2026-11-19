namespace Mind8Kernel
{
    public partial class Dispatcher
    {
        private void ZzKatu()
        {
            dtp.PopUl();
            uint right = dtp.PopUl();
            dtp.PopUl();
            uint left = dtp.PopUl();
            dtp.PushUl(left != 0 && right != 0 ? 1u : 0u);
            dtp.PushUl(0);
        }

        private void IsFalse() => dtp.SetUlOffset(dtp.GetUlOffset(1) == 0 ? 1u : 0u, 1);
        private void IsTrue() => dtp.SetUlOffset(dtp.GetUlOffset(1) != 0 ? 1u : 0u, 1);

        private void ZzMataha()
        {
            dtp.PopUl();
            uint right = dtp.PopUl();
            dtp.PopUl();
            uint left = dtp.PopUl();
            dtp.PushUl(left != 0 || right != 0 ? 1u : 0u);
            dtp.PushUl(0);
        }

        private void AmariToSyou()
        {
            dtp.PopUl();
            int divisor = unchecked((int)dtp.PopUl());
            dtp.PopUl();
            int dividend = unchecked((int)dtp.PopUl());
            int quotient = dividend / divisor;
            dtp.PushUl(unchecked((uint)(dividend % divisor)));
            dtp.PushUl(0);
            dtp.PushUl(unchecked((uint)quotient));
            dtp.PushUl(0);
        }

        private void Ijyou()
        {
            dtp.PopUl();
            int right = unchecked((int)dtp.PopUl());
            dtp.PopUl();
            int left = unchecked((int)dtp.PopUl());
            dtp.PushUl(left >= right ? 1u : 0u);
            dtp.PushUl(0);
        }

        private void Fusu()
        {
            dtp.PopUl();
            uint value = dtp.PopUl();
            dtp.PushUl(unchecked(0u - value));
            dtp.PushUl(0);
        }

        private void Fukusya()
        {
            uint high = dtp.GetUl();
            uint low = dtp.GetUlOffset(1);
            dtp.PushUl(low);
            dtp.PushUl(high);
        }

        private void IsFu() => dtp.SetUlOffset(unchecked((int)dtp.GetUlOffset(1)) < 0 ? 1u : 0u, 1);
        private void ZzAndWimm() => dtp.SetUlOffset(dtp.GetUlOffset(1) & mp.FetchMcode(), 1);

        private void Tiisai()
        {
            dtp.PopUl();
            int right = unchecked((int)dtp.PopUl());
            dtp.PopUl();
            int left = unchecked((int)dtp.PopUl());
            dtp.PushUl(left < right ? 1u : 0u);
            dtp.PushUl(0);
        }

        private void Ookii()
        {
            dtp.PopUl();
            int right = unchecked((int)dtp.PopUl());
            dtp.PopUl();
            int left = unchecked((int)dtp.PopUl());
            dtp.PushUl(left > right ? 1u : 0u);
            dtp.PushUl(0);
        }

        private void Hitosii()
        {
            dtp.PopUl();
            uint right = dtp.PopUl();
            dtp.PopUl();
            uint left = dtp.PopUl();
            dtp.PushUl(left == right ? 1u : 0u);
            dtp.PushUl(0);
        }

        private void Koukan()
        {
            uint secondHigh = dtp.PopUl();
            uint secondLow = dtp.PopUl();
            uint firstHigh = dtp.PopUl();
            uint firstLow = dtp.PopUl();
            dtp.PushUl(secondLow);
            dtp.PushUl(secondHigh);
            dtp.PushUl(firstLow);
            dtp.PushUl(firstHigh);
        }

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
			dtp.SetUlOffset(dtp.GetUlOffset(1)+mp.FetchMcode(), 1);
		}

		/* [n(32)を　加え]のコンパイリングワード */
		private void  ZzAddDimm()         	/* ;WORD $$ADD_DIMM */
		{
				/* Mコード=0x0106 */
			// L_OF_Q += FETCH_MCODE_LONG;
			dtp.SetUlOffset(dtp.GetUlOffset(1)+mp.FetchMcodeInt(), 1);
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
			dtp.SetUlOffset(dtp.GetUlOffset(1)-mp.FetchMcode(),1);
		}
		/* [n(32)を　引く]のコンパイリングワード */
		private void   ZzSubDimm()         	/* ;WORD $$SUB_DIMM */
		{
				/* Mコード=0x0109 */
			// L_OF_Q -= FETCH_MCODE_LONG;
			dtp.SetUlOffset(dtp.GetUlOffset(1)-mp.FetchMcodeInt(), 1);

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
			dtp.SetUlOffset((uint)(dtp.GetUlOffset(1)-data2), 1);
		}

        private void Waru()
        {
            dtp.PopUl();
            int divisor = unchecked((int)dtp.PopUl());
            int dividend = unchecked((int)dtp.GetUlOffset(1));
            dtp.SetUlOffset(unchecked((uint)(dividend / divisor)), 1);
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