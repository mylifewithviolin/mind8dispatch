using System.Text;
using System.Dynamic;
using System.Runtime.InteropServices;

namespace Mind8Kernel
{
    public partial class Dispatcher
    {
        private readonly int maxCountCsFunctions = 1 + 0x02F6;
        /// <summary>C#の関数配列を準備する</summary>
        /// <param name="csFunc">C#関数配列の参照</param>
        private void SetupCsFunctions(ref Action[] csFuncs){
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            csFuncs=new Action[maxCountCsFunctions];
        /*--------------------- 実行  -----------------------------------*/
            csFuncs[0x0010]=ZzEndExecute;//不完全要注意
            csFuncs[0x0011]=Jikkou;//不完全要注意
            csFuncs[0x0012]=Musyori;//
            csFuncs[0x0013]=EmgcyExit;
            csFuncs[0x0014]=ProcessOwari;
        /*--------------------- スタックポインタのアクセス  --------------*/
            csFuncs[0x0015]=StackKensa0;
            csFuncs[0x0016]=StackReset;//
            csFuncs[0x0017]=FinishDispatcher;
            csFuncs[0x0018]=C_stack_data_no;
        /*--------------------- $$RESERVE[nL]  --------------------------*/
            csFuncs[0x0019]=ZzReserve1L;
            csFuncs[0x001A]=ZzReserve2L;
            csFuncs[0x001B]=ZzReserve3L;
            csFuncs[0x001C]=ZzReserve4L;
            csFuncs[0x001D]=ZzReserve5L;
            csFuncs[0x001E]=ZzReserve6L; 
            csFuncs[0x001F]=ZzReserve; 
        /*--------------------- $$EXIT[nL]  -----------------------------*/
            csFuncs[0x0020]=ZzExit1L;//
            csFuncs[0x0021]=ZzExit2L;
            csFuncs[0x0022]=ZzExit3L;
            csFuncs[0x0023]=ZzExit4L;
            csFuncs[0x0024]=ZzExit5L;
            csFuncs[0x0025]=ZzExit6L;
            csFuncs[0x0026]=ZzExit7L;
            csFuncs[0x0027]=ZzExit;
        /*--------------------- $$IFEXIT系  -----------------------------*/
            csFuncs[0x0028]=ZzIfExit;//
            csFuncs[0x0029]=ZzIf0Exit;//
            csFuncs[0x002A]=ZzIf1Exit;//
            csFuncs[0x002B]=ZzNotIfExit;//
            csFuncs[0x002C]=ZzNotIf0Exit;//
            csFuncs[0x002D]=ZzNotIf1Exit;//
        /*--------------------- $$JMP  ----------------------------------*/
            csFuncs[0x002E]=ZzJmpRelative; 
            csFuncs[0x002F]=ZzJmpIndexed; //

            csFuncs[0x0030]=ZzPointNull;
            csFuncs[0x0031]=ZzPushNull;
            csFuncs[0x0032]=ZzPushNull;//重複
            csFuncs[0x0033]=ZzPushPointer;
            csFuncs[0x0034]=ZzPushPointerPlusSize;
            csFuncs[0x0035]=AddressWoEru;
        /*---------------------  大域変数のセットアップ  ------------------*/
            csFuncs[0x0036]=ZzPointVarAddr;//
            csFuncs[0x0037]=ZzPointCodeAddr;
        /*---------------------  大域集合のセットアップ  ------------------*/
            csFuncs[0x0038]=ZzPushVarAddrPlusSize;
            csFuncs[0x0039]=ZzPushMcodeAddrPlusSize;
        /*---------------------  局所変数のセットアップ  ------------------*/
            csFuncs[0x003A]=ZzPointLocalAddr0W;
            csFuncs[0x003B]=ZzPointLocalAddr1W;
            csFuncs[0x003C]=ZzPointLocalAddr2W;
            csFuncs[0x003D]=ZzPointLocalAddr3W;
            csFuncs[0x003E]=ZzPointLocalAddr4W; 
            csFuncs[0x003F]=ZzPointLocalAddr5W; 
            
            csFuncs[0x0040]=ZzPointLocalAddr6W;
            csFuncs[0x0041]=ZzPointLocalAddr7W;
            csFuncs[0x0042]=ZzPointLocalAddr;
        /*---------------------  局所集合のセットアップ  ------------------*/
            csFuncs[0x0043]=ZzPushLocalAddrPlusSize;
            csFuncs[0x0044]=ZzPushLocalAddrPlusSize;
        /*---------------------  変数の間接読み出し  ----------------------*/
            csFuncs[0x0045]=ZzPushBvar;
            csFuncs[0x0046]=ZzPushWvar;
            csFuncs[0x0047]=ZzPushDvar;
            csFuncs[0x0048]=ZzPushLvar;
            csFuncs[0x0049]=ZzPushQvar;
            csFuncs[0x004A]=ZzPushSvar;
            csFuncs[0x004B]=ZzPushJvar;
        /*---------------------  変数の直接読み出し  ----------------------*/
            csFuncs[0x004C]=ZzReadBvar;
            csFuncs[0x004D]=ZzReadWvar;
            csFuncs[0x004E]=ZzReadDvar;
            csFuncs[0x004F]=ZzReadLvar; 
            csFuncs[0x0050]=ZzReadQvar; 

            csFuncs[0x0051]=ZzReadSvar;
            csFuncs[0x0052]=ZzReadJvar;
        /*---------------------  局所変数の直接読み出し  ---------------*/
            csFuncs[0x0053]=ZzReadBvarLocal;
            csFuncs[0x0054]=ZzReadWvarLocal;
            csFuncs[0x0055]=ZzReadDvarLocal;
            csFuncs[0x0056]=ZzReadLvarLocal;
            csFuncs[0x0057]=ZzReadQvarLocal;
            csFuncs[0x0058]=ZzReadSvarLocal;
            csFuncs[0x0059]=ZzReadJvarLocal;
        /*---------------------  変数の間接書き込み  ------------------*/
            csFuncs[0x005A]=ZzPopBvar;
            csFuncs[0x005B]=ZzPopWvar;
            csFuncs[0x005C]=ZzPopDvar;
            csFuncs[0x005D]=ZzPopLvar;
            csFuncs[0x005E]=ZzPopQvar;
            csFuncs[0x005F]=ZzPopSvar;

            csFuncs[0x0060]=ZzPopMemory;
            csFuncs[0x0061]=ZzPopMemory;
            csFuncs[0x0062]=ZzPopMemoryWithResult;
        /*---------------------  変数の直接書き込み  --------------------*/
            csFuncs[0x0063]=ZzWriteBvar;//
            csFuncs[0x0064]=ZzWriteWvar;//
            csFuncs[0x0065]=ZzWriteDvar;//
            csFuncs[0x0066]=ZzWriteLvar;//
            csFuncs[0x0067]=ZzWriteQvar;//
            csFuncs[0x0068]=ZzWriteSvar;//
        /*---------------------  局所変数の直接書き込み  ----------------*/
            csFuncs[0x0069]=ZzWriteBvarLocal;
            csFuncs[0x006A]=ZzWriteWvarLocal;
            csFuncs[0x006B]=ZzWriteDvarLocal;
            csFuncs[0x006C]=ZzWriteLvarLocal;
            csFuncs[0x006D]=ZzWriteQvarLocal;
            csFuncs[0x006E]=ZzWriteSvarLocal;
        /*---------------------  変数のクリア  --------------------------*/
            csFuncs[0x006F]=ZzClearBvar;//
            csFuncs[0x0070]=ZzClearWvar;//
            csFuncs[0x0071]=ZzClearDvar;//
            csFuncs[0x0072]=ZzClearLvar;//
            csFuncs[0x0073]=ZzClearFvar;
            csFuncs[0x0074]=ZzClearSvar;//
            csFuncs[0x0075]=ZzClearMemory;
            csFuncs[0x0076]=ZzClearMemory;
        /*--------------------- 変数を一つ増加  -------------------------*/
            csFuncs[0x0077]=ZzIncBvar;
            csFuncs[0x0078]=ZzIncWvar;
            csFuncs[0x0079]=ZzIncDvar;
            csFuncs[0x007A]=ZzIncLvar;
        /*--------------------- 変数を一つ減少  -------------------------*/
            csFuncs[0x007B]=ZzDecBvar;
            csFuncs[0x007C]=ZzDecWvar;
            csFuncs[0x007D]=ZzDecDvar;
            csFuncs[0x007E]=ZzDecLvar;
        /*--------------------- 変数をセット  ---------------------------*/
            csFuncs[0x007F]=ZzSetBvar;//
            csFuncs[0x0080]=ZzSetWvar;//
            csFuncs[0x0081]=ZzSetDvar;//
            csFuncs[0x0082]=ZzSetLvar;//

            csFuncs[0x0083]=c_test_push_quad;
            csFuncs[0x0084]=c_test_push_float;
        /*--------------------- 変数を増加  -----------------------------*/
            csFuncs[0x0085]=ZzAddBvar;
            csFuncs[0x0086]=ZzAddWvar;
            csFuncs[0x0087]=ZzAddDvar;
            csFuncs[0x0088]=ZzAddLvar;
            csFuncs[0x0089]=ZzAddFvar;
        /*--------------------- 変数を減少  -----------------------------*/
            csFuncs[0x008A]=ZzSubBvar;
            csFuncs[0x008B]=ZzSubWvar;
            csFuncs[0x008C]=ZzSubDvar;
            csFuncs[0x008D]=ZzSubLvar;
            csFuncs[0x008E]=ZzSubFvar;
        /*--------------------- バイトロード～ダブルロード  ----------------*/
            csFuncs[0x008F]=ByteLoad;
            csFuncs[0x0090]=WordLoad;
            csFuncs[0x0091]=DoubleLoad;
        /*--------------------- バイトストア～ダブルストア  ----------------*/
            csFuncs[0x0092]=ByteStore;
            csFuncs[0x0093]=WordStore;
            csFuncs[0x0094]=DoubleStore;  
        /*--------------------- 数値定数  -------------------------------*/
            csFuncs[0x0095]=Zz0;//
            csFuncs[0x0096]=Zz1;//
            csFuncs[0x0097]=Zz2;//
            csFuncs[0x0098]=Zz3;//
            csFuncs[0x0099]=Zz4;//
            csFuncs[0x009A]=Zz5;//
            csFuncs[0x009B]=Zz6;//
            csFuncs[0x009C]=Zz7;//
            csFuncs[0x009D]=Zz8;//
            csFuncs[0x009E]=Zz9;//
            csFuncs[0x009F]=Zz10;//
            csFuncs[0x00A0]=ZzWord;//
            csFuncs[0x00A1]=ZzMword;//
            csFuncs[0x00A2]=ZzDouble;//
            csFuncs[0x00A3]=ZzQuad;//
        /*--------------------- 文字列定数  ----------------------------*/
            csFuncs[0x00A4]=ZzSliteralOdd;//
            csFuncs[0x00A5]=ZzSliteralEven;//
            csFuncs[0x00A6]=C_atof;
            csFuncs[0x00A7]=CMZero;
            csFuncs[0x00A8]=CMOne;
            csFuncs[0x00A9]=CME;
            csFuncs[0x00AA]=CMLog2E;
            csFuncs[0x00AB]=CMLog10E;
            csFuncs[0x00AC]=CMLn2;
            csFuncs[0x00AD]=CMLn10;
            csFuncs[0x00AE]=CMPi;
            csFuncs[0x00AF]=CMPi2;
            csFuncs[0x00B0]=CMPi4;
            csFuncs[0x00B1]=CM1Pi;
            csFuncs[0x00B2]=CM2Pi;
            csFuncs[0x00B3]=CM2SqrtPi;
            csFuncs[0x00B4]=CMSqrt2;
            csFuncs[0x00B5]=CM_SQRT1_2;
            csFuncs[0x00B6]=StackPointer;
            csFuncs[0x00B7]=ReturnStackPointer;
            csFuncs[0x00B8]=zzDataArear;
            csFuncs[0x00B9]=seisubuWoEru;
            csFuncs[0x00BA]=syosubuWoEru;
            csFuncs[0x00BB]=seisubuWoClear;
            csFuncs[0x00BC]=seisuToSyosuWoPack;
            csFuncs[0x00BD]=seisuToSyosuWoUnpack;
        /*--------------------- 文字列情報分解・合成  -------------------*/
            csFuncs[0x00BE]=MojiretuJyouhouBunkai;//
            csFuncs[0x00BF]=MojiretuJyouhouGousei;//
        /*--------------------- 表示系  --------------------------------*/
            csFuncs[0x00C0]=WordStore11;
            csFuncs[0x00C1]=ZzHyouji;//
            csFuncs[0x00C2]=ZzDoubleQuateHyouji;
            csFuncs[0x00C3]=ZzIchimojiHyouji;
            csFuncs[0x00C4]=ZzKaigyou;
            csFuncs[0x00C5]=ZzNibbleHyouji;
            csFuncs[0x00C6]=ZzByteHyouji;
            csFuncs[0x00C7]=ZzWordHyouji;
            csFuncs[0x00C8]=ZzDoubleHyouji;
            csFuncs[0x00CB]=zzSyosuHyouji;
            csFuncs[0x00CC]=zzSisuHyouji;
            csFuncs[0x00CD]=C_conv_to_string_fraction;
            csFuncs[0x00CE]=zzQuadHyouji;
            csFuncs[0x00CF]=zzDump;
            csFuncs[0x00D0]=zzDumpWord;
            csFuncs[0x00D1]=zzStackDump;
            csFuncs[0x00D2]=zzReturnStackMonitor;
            csFuncs[0x00C9]=ZzBaiseidoHyouji;
            csFuncs[0x00CA]=C_conv_to_string_quad;
            csFuncs[0x00D3]=ZzPointBarray;
            csFuncs[0x00D4]=ZzPointWarray;
            csFuncs[0x00D5]=ZzPointDarray;
            csFuncs[0x00D6]=ZzPointQarray;
            csFuncs[0x00D7]=ZzPointQarray;
            csFuncs[0x00D8]=ZzPointJXarray;
            csFuncs[0x00D9]=ZzPointBarrayLocal;
            csFuncs[0x00DA]=ZzPointWarrayLocal;
            csFuncs[0x00DB]=ZzPointDarrayLocal;
            csFuncs[0x00DC]=ZzPointQarrayLocal;
            csFuncs[0x00DD]=ZzPointQarrayLocal;
            csFuncs[0x00DE]=ZzPointJXarrayLocal;
            csFuncs[0x00DF]=ZzPointConsBarray;
            csFuncs[0x00E0]=ZzPointConsWarray;
            csFuncs[0x00E1]=ZzPointConsDarray;
            csFuncs[0x00E2]=ZzPointConsQarray;
            csFuncs[0x00E3]=ZzPointConsJarray;
            csFuncs[0x00E4]=ZzPointStrucVariable;
            csFuncs[0x00E5]=ZzPushStrucRecord;
            csFuncs[0x00E6]=ZzPointStrucVariableLocal;
            csFuncs[0x00E7]=ZzPushStrucRecordLocal;
            csFuncs[0x00E8]=ZzRepointTempVariableShort;
            csFuncs[0x00E9]=ZzRepointTempRecordShort;
            csFuncs[0x00EA]=ZzRepointTempVariable;
            csFuncs[0x00EB]=ZzRepointTempRecord;
            
        /*--------------------- ならば～つぎに  -------------------------*/
            csFuncs[0x00EC]=Zzif;//
            csFuncs[0x00ED]=ZzNotif;//
        /*------------------- 回数指定～繰り返し  ------------------------*/
            csFuncs[0x00EE]=ZzDo;//
            csFuncs[0x00EF]=ZzLoop;
            csFuncs[0x00F0]=ZzBreakLoop;
            csFuncs[0x00F1]=Kaisu;
            csFuncs[0x00F2]=KaisuM1;
            csFuncs[0x00F3]=KaisuP1;
            csFuncs[0x00F4]=ZenKausu;
            csFuncs[0x00F5]=SotogawaKaisu;
            csFuncs[0x00F6]=SotogawaKaisuM1;
            csFuncs[0x00F7]=SotogawaKaisuP1;
            csFuncs[0x00F8]=SotogawaZenKausu;
            csFuncs[0x00F9]=ZzSelect;
            csFuncs[0x00FA]=ZzDcase;
            csFuncs[0x00FB]=ZzDrange;
            csFuncs[0x00FC]=ZzScase;

        /*--------------------- 数値計算  -------------------------------*/
            csFuncs[0x00FD]=ZzKatu;
            csFuncs[0x00FE]=ZzMataha;
            csFuncs[0x00FF]=IsTrue;
            csFuncs[0x0100]=IsFalse;
            csFuncs[0x0101]=HitotuKuwae;//
            csFuncs[0x0102]=FutatuKuwae;//
            csFuncs[0x0103]=HitotuHiki;//
            csFuncs[0x0104]=FutatuHiki;//
            csFuncs[0x0105]=ZzAddWimm;//
            csFuncs[0x0106]=ZzAddDimm;//
            csFuncs[0x0107]=Kuwae;//
            csFuncs[0x0108]=ZzSubWimm;//
            csFuncs[0x0109]=ZzSubDimm;//
            csFuncs[0x010A]=Hiku;//
            csFuncs[0x010C]=Waru;
            csFuncs[0x010D]=AmariToSyou;
            csFuncs[0x0110]=Tiisai;
            csFuncs[0x0111]=Ijyou;
            csFuncs[0x0112]=Ookii;
            csFuncs[0x0114]=Hitosii;
            csFuncs[0x011A]=Fusu;
            csFuncs[0x011C]=Fukusya;
            csFuncs[0x011E]=Koukan;
            csFuncs[0x0121]=IsFu;
            csFuncs[0x0138]=ZzAndWimm;
            // csFuncs[0x010B]=HitotuHiki;//
            // csFuncs[0x010C]=FutatuHiki;//
            csFuncs[0x0122]=Sute;//
            csFuncs[0x0123]=NibannmeWoSute;//
            csFuncs[0x0124]=FutatuSute;//
            // csFuncs[0x0125]=FutatuHiki;//   
        /*--------------------- 文字列実体変数への格納  ------------------*/
            csFuncs[0x015F]=IsKuretu;
            csFuncs[0x0160]=IsNotKuretu;
            csFuncs[0x0161]=ZzCtoS;
            csFuncs[0x0164]=ZzPopJvar;//
            csFuncs[0x0165]=ZzWriteJvar;//
            csFuncs[0x0166]=ZzWriteJvarLocal;//
            csFuncs[0x0167]=MojiretuJittaiWoIre;//        
            csFuncs[0x0168]=ZzClearJvar;
            csFuncs[0x0169]=ZzClearJvar;
            csFuncs[0x016A]=MojiretuJittaiWoSansyo;
            csFuncs[0x0170]=ChgDefaultStruct;
            csFuncs[0x0171]=ChgDefaultStruct;
            csFuncs[0x0173]=TuikaYoyu;
            csFuncs[0x0177]=ItiMojiTuika;
            csFuncs[0x017A]=ItiMojiHigaTuikaForJinfo;
            csFuncs[0x017B]=Tuika;
            csFuncs[0x0182]=MigiYoseForJinfo;
        /*----------- その他、文字列実体情報によるオペレーション  ---------*/
            csFuncs[0x016E]=ZzClearXvar;//
        /*--------------------- 一バイト検索  ---------------------------*/    
            csFuncs[0x01B7]=MigiKiridasi;
            csFuncs[0x01A5]=ItiMojiMigiKiridasi;
            csFuncs[0x01D2]=UtanMoji;
            csFuncs[0x01D5]=Toridasi;
            csFuncs[0x01D7]=ItiBaitoKensaku;
            csFuncs[0x01D8]=GyakuItiBaitoKensaku;
            csFuncs[0x01DD]=HitosiiMojiretu;
            csFuncs[0x01EC]=SizeHenko;
            csFuncs[0x01E9]=KonoBaitoSu;
        /*--------------------- 私のプログラム名  -----------------------*/
            csFuncs[0x0261]=CmcodeFullFilename;//
            csFuncs[0x0263]=GetArgN;
            csFuncs[0x0264]=GetArgC;//
            csFuncs[0x0268]=EnableCtrlCSignal;
        /*--------------------- 標準入出力  -----------------------------*/
            csFuncs[0x026D]=OutputDevice;//
            csFuncs[0x0296]=BinaryMode;
            csFuncs[0x0297]=TextMode;
            csFuncs[0x0298]=SetFileMode;
            csFuncs[0x02CD]=LowSecurityMode;
            csFuncs[0x02CE]=HighSecurityMode;
        }

    }
}