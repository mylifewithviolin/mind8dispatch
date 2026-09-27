using System.Dynamic;

namespace Mind8Kernel
{

    /// <summary>Mind8ディスパッチャ</summary>
    public partial class Dispatcher
    {
        
        private readonly int sizeOfMcodoInfo = 32;
        
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

            int retcode=0;//setjmp( RESTARTENV );		/* ←ディスパッチャ再 */
            if ( retcode != 2 )			/* 強制脱出の検査 */
            {
                try{
                    int i=0;
                    for(;;)
                    {
                        mcode = mp.FetchMcode();
                        Console.Write("{0,4:d} {1,4:X4} {2,6:d} {3,6:d}",++i,mcode,mcode,mp.GetMcodeIndex());
                        //Console.WriteLine(" {0,6:d} {1,8:d} {2,8:d} {3,8:d} {4,8:d} {5,8:d} {6,8:d} {7,6:d} {8,8:d} {9,8:d}",dtp.GetCurrentStackPointer(),dtp.GetUl(),
                        Console.WriteLine(" {0,6:d} {1,8:d} {2,8:d} {3,6:d} {4,8:d} {5,8:d}",dtp.GetCurrentStackPointer(),dtp.GetUl(),
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
                    retcode=ljmp.retCode;
                    //状態復帰
                }catch(ExitModule eMd){
                    return eMd.retCode;
                }
            }
            return 0;
        }

        /// <summary>ディスパッチャを準備する</summary>
        /// <param name="McodeInfo">MCode情報構造体の参照</param>
        /// <param name="mcodefilename">MCode中間コードファイル名</param>
        private bool SetupForDispatch(
            ref McodeInfoStruct McodeInfo,
            String mcodefilename){

            using FileStream fs = new(mcodefilename, FileMode.Open);
            fs.Seek(0-sizeOfMcodoInfo, SeekOrigin.End);
            byte[] mcodeInf = new byte[sizeOfMcodoInfo];
            int ret=fs.Read(mcodeInf, 0, sizeOfMcodoInfo);
            if(ret < sizeOfMcodoInfo){
                Console.WriteLine("Can't read Mcode-info({0}).", ret);
                return false;
            }

            SetMcodeInfo(ref McodeInfo,mcodeInf);
            //MonitorMcodeInfo(ref McodeInfo);
            if ( McodeInfo.minfoMark1 != 'M' || McodeInfo.minfoMark2 != 'C' )
            {
                Console.WriteLine("Illegal Mcode mark" );
                return false;
            }
            if ( McodeInfo.minfoMainExist == 0 )
            {
                Console.WriteLine("kermain: MAIN is not defined" );
                return false;
            }
            //その他のmcodeInfoチェック当面割愛
            //checkNeedToBeSwapped();関連の処理は当面割愛
            fs.Seek(0, SeekOrigin.Begin);//rewindMcodeFileと同等
            //readMcode;
            uint size = McodeInfo.minfoMcodeSize;
            McodeBase = new byte[size];
            ret=fs.Read(McodeBase, 0, (int)size);//符号無サイズ読み取り不足の可能性あり
            if(ret<size){
                Console.WriteLine("kermain: Can't read M-code({0})", ret);
            return false;
            }

            //readLoctable;
            size = McodeInfo.minfoLoctableSize;
            WordOffsetTable= new byte[size];
            ret=fs.Read(WordOffsetTable, 0, (int)size);//符号無サイズ読み取り不足の可能性あり
            if(ret<size){
                Console.WriteLine("kermain: Can't read Loc-table({0})", ret);
            return false;
            }
            //adjustLoctable();
            //McodeBaseはバイト配列なのでこの処理成立しない
            // uint entryNo = McodeInfo.minfoLoctableSize / 4;
            // for(int index=0; index<entryNo; index++ )
            // {
            //     WordOffsetTable[index] += (uint)McodeBase;
            // }
            //reserveDataArea();
            //reserveStackArea();
            /* スタックサイズの確定 */
            //ushort DataStackSize = (ushort)McodeInfo.minfoDstackSize;
            //ushort RetnStackSize = (ushort)McodeInfo.minfoRstackSize;
            //StackPointerで実装
            
            return true;
        }

        /// <summary>バイト配列からMCode情報構造体をセットする</summary>
        /// <param name="McodeInfo">MCode情報構造体の参照</param>
        /// <param name="mcodeInf">MCode情報構造体相当のバイト配列</param>
        private void SetMcodeInfo(ref McodeInfoStruct McodeInfo,byte[] mcodeInf){
            McodeInfo.minfoMark1= (char)mcodeInf[0];//1
            McodeInfo.minfoMark2= (char)mcodeInf[1];//2
            McodeInfo.minfoMark3= mcodeInf[2];//3
            McodeInfo.minfoMark4= mcodeInf[3];//4
            byte[] int16Byte = new byte[2];
            int i=0;
            for(i=0;i<2;i++)int16Byte[i]=mcodeInf[i+4];
            McodeInfo.minfoMainExist= BitConverter.ToUInt16(int16Byte, 0);//5-6
            for(i=0;i<2;i++)int16Byte[i]=mcodeInf[i+6];
            McodeInfo.minfoLimitD= BitConverter.ToUInt16(int16Byte, 0);//7-8
            byte[] int32Byte = new byte[4];
            for(i=0;i<4;i++)int32Byte[i]=mcodeInf[i+8];
            McodeInfo.minfoMcodeSize= BitConverter.ToUInt32(int32Byte, 0);//9-12
            for(i=0;i<4;i++)int32Byte[i]=mcodeInf[i+12];
            McodeInfo.minfoLoctableSize= BitConverter.ToUInt32(int32Byte, 0);	//13-16
            for(i=0;i<4;i++)int32Byte[i]=mcodeInf[i+16];
            McodeInfo.minfoDataSize= BitConverter.ToUInt32(int32Byte, 0);	//17-20
            McodeInfo.minfoRuntimeNo= mcodeInf[20];//21
            McodeInfo.minfoDummy21= (char)mcodeInf[21];//22
            for(i=0;i<2;i++)int16Byte[i]=mcodeInf[i+22];
            McodeInfo.minfoSerialNo= BitConverter.ToUInt16(int16Byte, 0);//23-24
            for(i=0;i<4;i++)int32Byte[i]=mcodeInf[i+24];
            McodeInfo.minfoDstackSize= BitConverter.ToUInt32(int32Byte, 0);//25-28
            for(i=0;i<4;i++)int32Byte[i]=mcodeInf[i+28];
            McodeInfo.minfoRstackSize= BitConverter.ToUInt32(int32Byte, 0);//29-32
        }
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
