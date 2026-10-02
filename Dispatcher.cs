using System.Buffers.Binary;

namespace Mind8Kernel
{

    /// <summary>Mind8ディスパッチャ</summary>
    public partial class Dispatcher
    {
        
        private const int McodeInfoSize = 32;
        private const byte BigEndianMcodeBit = 0x20;
        private const int MajorVersion = 8;
        private const byte RuntimeNumber = 10;
        private const ushort SerialNumber = 42;
        
        /// <summary>MCode情報構造体</summary>
        private struct McodeInfoStruct	     /*-- 全体=32バイト --*/
        {
            public char minfoMark1;//1
            public char minfoMark2;//2
            public byte  minfoMark3;//3
            public byte  minfoMark4;//4
            public ushort minfoMainExist;//5-6
            public ushort minfoLimitD;//7-8
            public uint minfoMcodeSize;//9-12		
            public uint minfoLoctableSize;	//13-16
            public uint minfoDataSize;	//17-20
            public byte  minfoRuntimeNo;//21
            public char  minfoDummy21;//22
            public ushort minfoSerialNo;//23-24
            public uint  minfoDstackSize;//25-28
            public uint minfoRstackSize;//29-32
        };
    //  typedef union
    // {
    //         UCHAR*  b;      /* for unsigned byte access */
    //         USHORT* w;      /* for unsigned word access */
    //         SHORT*  s;      /* for signed short access  */
    //         LONG*   l;      /* for signed long access   */
    //         ULONG*  u;      /* for unsigned long access */
    //         QWORD*  q;      /* for quad word accrss     */
    //        word64* ll;      /* for long long(64bit int) */
    //        double*  f;      /* for fraction access      */
    // }tGeneralPointer;     

        /// <summary>広域ジャンプ用のアプリケーション例外</summary>
        private class LongJmp(int ret) : Exception{
            public int retCode = ret;
        }
        /// <summary>プログラム終了用のアプリケーション例外</summary>
        private class ExitModule(int ret) : Exception{
            public int retCode=ret;
        } 
        /// <summary>プログラム終了用のアプリケーション例外　未実装</summary>
        private class UnDefined(int ret) : Exception{
            public int retCode=ret;
        } 

        private  byte[] McodeBase =[];//MCode領域
        private byte[] WordOffsetTable=[];//LOCテーブル
        private MCodePointer mp=new();//MCode領域用ポインタ
        private LocTablePointer lp=new();//LOCテーブル用ポインタ
        private AccessPointer ap =new(); //データ領域用ポインタ
        private StackPointer dtp =new(); //データスタック用ポインタ
        private StackPointer rtp =new(); //リターンスタック用ポインタ
        private string mcodefilename ="";
        private int argc=2;

        /// <summary>メイン</summary>
        /// <param name="args">引数</param>
        public int Main(string args)
        {
            mcodefilename=args;
            McodeInfoStruct mcodeInfo =new();

            /* ディスパッチャを準備する */
            bool ret=SetupForDispatch(ref mcodeInfo,mcodefilename);
            if(!ret){
                return -1;
            }

            //MonitorMcode(McodeBase,116,186);//MCode領域をモニタする
            mp.SetupMcodeArray(McodeBase);//MCode領域用ポインタを準備する
            lp.SetupLocArray(WordOffsetTable);//LOCテーブル用ポインタを準備する
            ap.SetupDataArray(mcodeInfo.minfoDataSize);//データ領域用ポインタを準備する
            //resetDataStackPointer();//DstackPointer = DataStackBase[RestartEnvCount];
            //resetRetnStackPointer();//RstackPointer = RetnStackBase;
            dtp.SetupStackArray((ushort)mcodeInfo.minfoDstackSize);//データスタック用ポインタを準備する
            rtp.SetupStackArray((ushort)mcodeInfo.minfoRstackSize);//リターンスタック用ポインタを準備する
            //McodePointer.b = (UCHAR *)McodeBase + 0x70;	/* 0x70=実行開始地点 */
            mp.ResetMcodeIndex(0x70);

            /* C#の関数を準備する */
            Action[] csFuncs=[]; //C#の関数を格納する配列
            /* C#の関数配列を準備する */
            SetupCsFunctions(ref csFuncs);
            /* ディスパッチャ開始 */
            return Dispatch(mcodeInfo, csFuncs);

        }

        /// <summary>ディスパッチャ</summary>
        /// <param name="McodeInfo">MCode情報構造体の参照</param>
        /// <param name="csFunc">C#関数配列の参照</param>
        private int Dispatch(McodeInfoStruct McodeInfo, Action[] csFuncs){
            
            ushort   mcode;

            while (true)
            {
                try{
                    int i=0;
                    for(;;)
                    {
                        mcode = mp.FetchMcode();
                        Console.Error.Write("{0,4:d} {1,4:X4} {2,6:d} {3,6:d}",++i,mcode,mcode,mp.GetMcodeIndex());
                        Console.Error.WriteLine(" {0,6:d} {1,8:d} {2,8:d} {3,6:d} {4,8:d} {5,8:d}",dtp.GetCurrentStackPointer(),dtp.GetUl(),
                                                    dtp.GetUlOffset(1),
                                                    // dtp.GetUlOffset(2),
                                                    // dtp.GetUlOffset(3),
                                                    // dtp.GetUlOffset(4),
                                                    // dtp.GetUlOffset(5),
                                                    rtp.GetCurrentStackPointer(),rtp.GetUl(),rtp.GetUlOffset(1));
      ;                  // int mpCurrentIndex= (int)mp.GetMcodeIndex();
                        // if(mpCurrentIndex<10){
                        //     MonitorMcode(McodeBase,0,mpCurrentIndex+20);//mcode±10の範囲のMCode領域をモニタする
                        // }else{
                        //     MonitorMcode(McodeBase,mpCurrentIndex-10,mpCurrentIndex+20);//mcode±10の範囲のMCode領域をモニタする
                        // }
                        if ( (mcode & 0x8000)==0 )
                        {
                            /* C#関数 */
                            csFuncs[mcode]();
                        }
                        else
                        {
                            /* Mind単語 */
                            //PUSH_R( McodePointer.l );
                            //#define  PUSH_R(addr) *(--RstackPointer)=(ULONG)(addr)
                            rtp.PushUl(mp.GetMcodeIndex());//現在のMCodeポインタの値をリターンスタックにプッシュする
                            //SET_MCODE_POINTER_BY_WORDNO( mcode & 0x7fff );
                            //McodePointer.b = (UCHAR *)(WordOffsetTable[wordNo])
                            mp.ResetMcodeIndex((uint)lp.GetSl((uint)(mcode & 0x7fff)));
                        }
                    }
                }catch(LongJmp ljmp){
                    if (ljmp.retCode == 2)
                    {
                        return 0;
                    }
                    continue;
                }catch(ExitModule eMd){
                    return eMd.retCode;
                }
            }
        }

        /// <summary>ディスパッチャを準備する</summary>
        /// <param name="McodeInfo">MCode情報構造体の参照</param>
        /// <param name="mcodefilename">MCode中間コードファイル名</param>
        private bool SetupForDispatch(
            ref McodeInfoStruct McodeInfo,
            String mcodefilename){

            try
            {
                string resolvedFilename = ResolveMcodeFilename(mcodefilename);
                using FileStream fs = new(resolvedFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < McodeInfoSize)
                {
                    return Fail("M-code file is shorter than its information table.");
                }

                fs.Position = fs.Length - McodeInfoSize;
                byte[] mcodeInfoBytes = new byte[McodeInfoSize];
                fs.ReadExactly(mcodeInfoBytes);

                bool fileIsBigEndian = (mcodeInfoBytes[2] & BigEndianMcodeBit) != 0;
                bool hostIsBigEndian = !BitConverter.IsLittleEndian;
                bool needsByteSwap = fileIsBigEndian != hostIsBigEndian;
                SetMcodeInfo(ref McodeInfo, mcodeInfoBytes, fileIsBigEndian);

                if (!ValidateMcodeInfo(McodeInfo))
                {
                    return false;
                }

                ulong payloadSize = (ulong)(fs.Length - McodeInfoSize);
                ulong mcodeEnd = McodeInfo.minfoMcodeSize;
                ulong locTableEnd = mcodeEnd + McodeInfo.minfoLoctableSize;
                if (locTableEnd > payloadSize)
                {
                    return Fail("M-code and LOC table sizes exceed the file payload.");
                }

                McodeBase = new byte[checked((int)McodeInfo.minfoMcodeSize)];
                WordOffsetTable = new byte[checked((int)McodeInfo.minfoLoctableSize)];
                fs.Position = 0;
                fs.ReadExactly(McodeBase);
                fs.ReadExactly(WordOffsetTable);

                if (needsByteSwap)
                {
                    if ((McodeInfo.minfoMcodeSize & 7) != 0)
                    {
                        return Fail("M-code size is not aligned for its endian information table.");
                    }

                    int endianInfoSize = checked((int)(McodeInfo.minfoMcodeSize / 8));
                    if (locTableEnd + (uint)endianInfoSize > payloadSize)
                    {
                        return Fail("M-code endian information table is truncated.");
                    }

                    byte[] endianInfo = new byte[endianInfoSize];
                    fs.Position = checked((long)locTableEnd);
                    fs.ReadExactly(endianInfo);
                    if (!SwapMcode(McodeBase, endianInfo))
                    {
                        return false;
                    }
                    SwapLocTable(WordOffsetTable);
                }

                return true;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or OverflowException)
            {
                return Fail(exception.Message);
            }
        }

        private static string ResolveMcodeFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException("M-code filename is empty.", nameof(filename));
            }

            return string.Equals(Path.GetExtension(filename), ".mco", StringComparison.OrdinalIgnoreCase)
                ? filename
                : filename + ".mco";
        }

        private static bool ValidateMcodeInfo(McodeInfoStruct McodeInfo)
        {
            if (McodeInfo.minfoMark1 != 'M' || McodeInfo.minfoMark2 != 'C')
            {
                return Fail("Illegal M-code mark.");
            }
            if ((McodeInfo.minfoMark4 >> 4) != MajorVersion)
            {
                return Fail("M-code version does not match this runtime.");
            }
            if (McodeInfo.minfoRuntimeNo != RuntimeNumber)
            {
                return Fail("M-code runtime number does not match this runtime.");
            }
            if (McodeInfo.minfoSerialNo != SerialNumber)
            {
                return Fail("M-code serial number does not match this runtime.");
            }
            if (McodeInfo.minfoMainExist == 0)
            {
                return Fail("MAIN is not defined.");
            }
            if (McodeInfo.minfoMcodeSize < 0x72 || (McodeInfo.minfoMcodeSize & 1) != 0)
            {
                return Fail("M-code size is invalid.");
            }
            if (McodeInfo.minfoLoctableSize == 0 || (McodeInfo.minfoLoctableSize & 3) != 0)
            {
                return Fail("LOC table size is invalid.");
            }
            if (McodeInfo.minfoDataSize > int.MaxValue)
            {
                return Fail("Data area size exceeds the supported limit.");
            }
            if (McodeInfo.minfoDstackSize == 0 || McodeInfo.minfoDstackSize > ushort.MaxValue
                || (McodeInfo.minfoDstackSize & 3) != 0)
            {
                return Fail("Data stack size is invalid for this runtime.");
            }
            if (McodeInfo.minfoRstackSize == 0 || McodeInfo.minfoRstackSize > ushort.MaxValue
                || (McodeInfo.minfoRstackSize & 3) != 0)
            {
                return Fail("Return stack size is invalid for this runtime.");
            }

            return true;
        }

        private static bool SwapMcode(byte[] mcode, byte[] endianInfo)
        {
            int wordIndex = 0;
            int pendingLongOffset = -1;
            foreach (byte infoByte in endianInfo)
            {
                for (int wordInGroup = 0; wordInGroup < 4; wordInGroup++, wordIndex++)
                {
                    int code = (infoByte >> (6 - wordInGroup * 2)) & 0x03;
                    int byteOffset = wordIndex * 2;
                    if (pendingLongOffset >= 0)
                    {
                        Array.Reverse(mcode, pendingLongOffset, 4);
                        pendingLongOffset = -1;
                    }
                    else if (code == 1)
                    {
                        Array.Reverse(mcode, byteOffset, 2);
                    }
                    else if (code == 2)
                    {
                        pendingLongOffset = byteOffset;
                    }
                }
            }

            if (pendingLongOffset >= 0)
            {
                return Fail("M-code endian information ends in the middle of a 32-bit value.");
            }
            return true;
        }

        private static void SwapLocTable(byte[] locTable)
        {
            for (int offset = 0; offset < locTable.Length; offset += 4)
            {
                Array.Reverse(locTable, offset, 4);
            }
        }

        private static bool Fail(string message)
        {
            Console.Error.WriteLine($"M-code load error: {message}");
            return false;
        }

        /// <summary>バイト配列からMCode情報構造体をセットする</summary>
        /// <param name="McodeInfo">MCode情報構造体の参照</param>
        /// <param name="mcodeInf">MCode情報構造体相当のバイト配列</param>
        private void SetMcodeInfo(ref McodeInfoStruct McodeInfo, byte[] mcodeInf, bool bigEndian){
            McodeInfo.minfoMark1= (char)mcodeInf[0];//1
            McodeInfo.minfoMark2= (char)mcodeInf[1];//2
            McodeInfo.minfoMark3= mcodeInf[2];//3
            McodeInfo.minfoMark4= mcodeInf[3];//4
            ReadOnlySpan<byte> info = mcodeInf;
            McodeInfo.minfoMainExist= ReadUInt16(info, 4, bigEndian);//5-6
            McodeInfo.minfoLimitD= ReadUInt16(info, 6, bigEndian);//7-8
            McodeInfo.minfoMcodeSize= ReadUInt32(info, 8, bigEndian);//9-12
            McodeInfo.minfoLoctableSize= ReadUInt32(info, 12, bigEndian);//13-16
            McodeInfo.minfoDataSize= ReadUInt32(info, 16, bigEndian);//17-20
            McodeInfo.minfoRuntimeNo= mcodeInf[20];//21
            McodeInfo.minfoDummy21= (char)mcodeInf[21];//22
            McodeInfo.minfoSerialNo= ReadUInt16(info, 22, bigEndian);//23-24
            McodeInfo.minfoDstackSize= ReadUInt32(info, 24, bigEndian);//25-28
            McodeInfo.minfoRstackSize= ReadUInt32(info, 28, bigEndian);//29-32
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset, bool bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(source.Slice(offset, 2)) : BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2));

        private static uint ReadUInt32(ReadOnlySpan<byte> source, int offset, bool bigEndian) =>
            bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(source.Slice(offset, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
        /// <summary>MCode情報構造体をモニタする</summary>
        /// <param name="McodeInfo">MCode情報構造体の参照</param>
        private void MonitorMcodeInfo(ref McodeInfoStruct McodeInfo){
            Console.WriteLine("McodeInfo.minfoMark1({0}).",McodeInfo.minfoMark1);
            Console.WriteLine("McodeInfo.minfoMark2({0}).",McodeInfo.minfoMark2);
            Console.WriteLine("McodeInfo.minfoMark3({0}).",McodeInfo.minfoMark3);
            Console.WriteLine("McodeInfo.minfoMark4({0}).",McodeInfo.minfoMark4);
            Console.WriteLine("McodeInfo.minfoLoctableSize({0}).",McodeInfo.minfoLoctableSize);
            Console.WriteLine("McodeInfo.minfoMainExist({0}).",McodeInfo.minfoMainExist);
            Console.WriteLine("McodeInfo.minfoMcodeSize({0}).",McodeInfo.minfoMcodeSize);
            Console.WriteLine("McodeInfo.minfoRuntimeNo({0}).",McodeInfo.minfoRuntimeNo);
            Console.WriteLine("McodeInfo.minfoSerialNo({0}).",McodeInfo.minfoSerialNo);
            Console.WriteLine("McodeInfo.minfoDstackSize({0}).",McodeInfo.minfoDstackSize);
            Console.WriteLine("McodeInfo.minfoRstackSize({0}).",McodeInfo.minfoRstackSize);
        }

        /// <summary>MCode領域をモニタする</summary>
        private void MonitorMcode(byte[] McodeBase,int start,int end){
            for(int i=start;i<end;i++){//最初の116バイト
                if(i%2==0){
                    byte[] int16Byte = new byte[2];
                    for(int j=0;j<2;j++)int16Byte[j]=McodeBase[i+j];
                    ushort mcode =BitConverter.ToUInt16(int16Byte, 0);
                    Console.Write(mcode.ToString("X4"));
                    Console.WriteLine(" {0}",mcode);
                }
            }
        }

    }
}
