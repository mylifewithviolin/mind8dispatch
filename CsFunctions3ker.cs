using System.Text;
using System.Runtime.InteropServices;

namespace Mind8Kernel
{
    public partial class Dispatcher
    {
        private bool UseLowSecurity { get; set; }
        private void LowSecurityMode() => UseLowSecurity = true;
        private void HighSecurityMode() => UseLowSecurity = false;

        private void EnableCtrlCSignal(){
            if(!Console.IsInputRedirected){
                Console.TreatControlCAsInput = false;
            }
        }
        private void BinaryMode(){
            dtp.PushUl(0x8000);
            dtp.PushUl(0);
        }
        private void TextMode(){
            dtp.PushUl(0x4000);
            dtp.PushUl(0);
        }
        [DllImport("ucrtbase.dll", EntryPoint = "_setmode", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SetNativeFileMode(int handle, int mode);

        private void SetFileMode(){
            dtp.PopUl();
            int mode = (int)dtp.PopUl();
            dtp.PopUl();
            int handle = (int)dtp.PopUl();
            if(!OperatingSystem.IsWindows()){
                throw new PlatformNotSupportedException("CRT file modes require Windows.");
            }
            if(SetNativeFileMode(handle, mode) == -1){
                throw new IOException($"Cannot set file mode 0x{mode:X} for handle {handle}.");
            }
        }
        /*--------------------- ���̃v���O������  -----------------------*/
        private void CmcodeFullFilename(){		/* ;WORD c_mcodeFullFilename */
                /* M�R�[�h=0x0261 */
            //PUSH_S( FullFilename, 0 );
            //#define  PUSH_S(addr,count)   PUSH_A(addr),PUSH_C(count)
            // #define  PUSH_L(value)        *(--DstackPointer)=(LONG)(value)
            // #define  PUSH_U(value)        PUSH_L(value)
            // #define  PUSH_A(addr)         PUSH_U(addr)
            // #define  PUSH_C(count)        PUSH_U(count)
            byte[] filenameBytes = Encoding.GetEncoding("shift_jis").GetBytes(mcodefilename + '\0');
            dtp.PushUl(mp.AppendBytes(filenameBytes));
            dtp.PushUl(0);
        }
        private void GetArgC()		/* ;WORD �������� */
        {
            /* M�R�[�h=0x0264 */
            /* (. ���@argc) */
            //PUSH_Q( Argc, 0 );
            dtp.PushUl((uint)argumentAddresses.Length);
            dtp.PushUl(0);
        }
        private void SetupArguments(string[] arguments)
        {
            string[] argv = [Environment.GetCommandLineArgs()[0],
                Path.GetFullPath(ResolveMcodeFilename(mcodefilename)), ..arguments];
            argumentAddresses = new uint[argv.Length];
            for (int i = 0; i < argv.Length; i++)
                argumentAddresses[i] = mp.AppendBytes(Encoding.GetEncoding("shift_jis").GetBytes(argv[i] + '\0'));
        }

        private void GetArgN()
        {
            dtp.PopUl();
            uint index = dtp.PopUl();
            if (index >= argumentAddresses.Length)
                throw new ArgumentOutOfRangeException(nameof(index), "Argument index is outside argc.");
            dtp.PushUl(argumentAddresses[index]);
            dtp.PushUl(0);
        }
        /*--------------------- �W�����o��  -----------------------------*/
        private void OutputDevice()	/* ;WORD �t�@�C���n���h���w��ŏo�� */
        {
                /* M�R�[�h=0x026D */
            /* (strinfo, filehandle �� .) */
            //     int		 filehandle;
            //     size_t		 length;
            //     UCHAR		*string;
            //#define  POP_U                ((ULONG)(*DstackPointer++))
            //#define  POP_U_OF_Q           (DROP_F,POP_U)
            // filehandle = POP_U_OF_Q;
            // length = POP_C;
            // string = POP_A;
            // write( filehandle, string, length );	/* �G���[�͌��Ȃ� */
            dtp.PopUl();
            uint filehandle=dtp.PopUl();
            uint length=dtp.PopUl();
            uint index=dtp.PopUl();              
            byte[] chars=ReadStringBytes(index,length);
            string text = Encoding.GetEncoding("shift_jis").GetString(chars);
            TextWriter writer = filehandle switch
            {
                1 => Console.Out,
                2 => Console.Error,
                _ => throw new NotSupportedException($"Output handle {filehandle} is not supported.")
            };
            writer.Write(text);
        }

    }
}