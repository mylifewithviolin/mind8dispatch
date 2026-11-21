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
                rtp.PushUl(1);
                mp.MoveMcodeIndex(1);
            }
        }

        private void ZzLoop()
        {
            uint count = rtp.GetUl();
            if (count >= rtp.GetUlOffset(1))
            {
                rtp.MoveStackPointer(2);
                mp.MoveMcodeIndex(1);
            }
            else
            {
                rtp.SetUl(count + 1);
                mp.MoveMcodeIndexByByte(mp.ReadMcodeShort());
            }
        }

        private void ZzBreakLoop()
        {
            rtp.MoveStackPointer(2);
            mp.MoveMcodeIndexByByte(mp.ReadMcodeShort());
        }

        private void Kaisu()
        {
            dtp.PushUl(rtp.GetUl());
            dtp.PushUl(0);
        }

        private void KaisuM1() => PushReturnCount(0, -1);
        private void KaisuP1() => PushReturnCount(0, 1);
        private void ZenKausu() => PushReturnCount(1, 0);
        private void SotogawaKaisu() => PushReturnCount(2, 0);
        private void SotogawaKaisuM1() => PushReturnCount(2, -1);
        private void SotogawaKaisuP1() => PushReturnCount(2, 1);
        private void SotogawaZenKaisu() => PushReturnCount(3, 0);

        private void PushReturnCount(int depth, int adjustment)
        {
            uint available = checked(rtp.StackSize / 4 - rtp.GetCurrentStackPointer());
            if ((uint)depth >= available)
                throw new InvalidOperationException($"Return stack does not contain count level {depth + 1}.");
            dtp.PushUl(unchecked(rtp.GetUlOffset(depth) + (uint)adjustment));
            dtp.PushUl(0);
        }

        private void ZzSelect()
        {
            uint table = RelativeTableAddress();
            ushort elements = ReadMcodeUshort(table);
            uint index = PopArrayIndex(elements);
            uint offsets = AddAddress(table, 2);
            mp.ResetMcodeIndex(RelativeMcodeAddress(offsets, ReadMcodeShort(offsets, index)));
        }

        private void ZzDcase() => BranchOnNumericCase(range: false);
        private void ZzDrange() => BranchOnNumericCase(range: true);

        private void BranchOnNumericCase(bool range)
        {
            uint table = RelativeTableAddress();
            ushort elements = ReadMcodeUshort(table);
            if (elements == 0) throw new InvalidDataException("Numeric case table has no default entry.");

            int value = unchecked((int)PopIntegerOfQuad());
            uint offsets = AddAddress(table, 2);
            for (uint index = 0; index < elements; index++)
            {
                uint branch = RelativeMcodeAddress(offsets, ReadMcodeShort(offsets, index));
                mp.ResetMcodeIndex(branch);
                if (index == elements - 1) return;

                int caseValue = unchecked((int)mp.FetchMcodeInt());
                if (range ? value <= caseValue : value == caseValue) return;
            }
        }

        private void ZzScase()
        {
            uint table = RelativeTableAddress();
            ushort elements = ReadMcodeUshort(table);
            if (elements == 0) throw new InvalidDataException("String case table has no default entry.");

            uint length = dtp.PopUl();
            uint address = dtp.PopUl();
            byte[] value = ReadStringBytes(address, length);
            uint offsets = AddAddress(table, 2);
            for (uint index = 0; index < elements; index++)
            {
                uint branch = RelativeMcodeAddress(offsets, ReadMcodeShort(offsets, index));
                mp.ResetMcodeIndex(branch);
                if (index == elements - 1) return;

                uint candidateLength = mp.FetchMcode();
                if (candidateLength == length
                    && mp.GetUb(mp.GetMcodeIndex(), candidateLength).AsSpan().SequenceEqual(value))
                {
                    uint paddedLength = checked((candidateLength + 1) & 0xFFFFFFFE);
                    mp.MoveMcodeIndexByByte(checked((int)paddedLength));
                    return;
                }

                uint skipLength = checked((candidateLength + 1) & 0xFFFFFFFE);
                mp.MoveMcodeIndexByByte(checked((int)skipLength));
            }
        }

        private uint RelativeTableAddress() =>
            RelativeMcodeAddress(mp.GetMcodeIndex(), mp.ReadMcodeShort());

        private ushort ReadMcodeUshort(uint address) =>
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(mp.GetUb(address, 2));

        private short ReadMcodeShort(uint address, uint index) =>
            System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(
                mp.GetUb(AddAddress(address, checked(index * 2)), 2));

        private uint RelativeMcodeAddress(uint address, short displacement)
        {
            long target = checked((long)address + displacement);
            if (target < 0 || target > AddressMask)
                throw new ArgumentOutOfRangeException(nameof(displacement), "Relative M-code target is outside the code region.");
            return (uint)target;
        }

    }
}