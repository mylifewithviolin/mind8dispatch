using System.Text;

namespace Mind8Kernel
{
    public partial class Dispatcher
    {

        /*--------------------- ��o�C�g����  ---------------------------*/
        private void ItiBaitoKensaku(){	  /* ;WORD ��o�C�g���� SN  ����:NX */
                /* M�R�[�h=0x01D7 */
            // UCHAR		*string;
            // size_t		 length;
            // UCHAR		 targByte;
            // size_t		 charPos;

            // targByte = POP_U_OF_Q;
            // POP_length_and_string;
            // charPos = searchByteDirect_Forward( string, length, targByte );
            // PUSH_Q( charPos, 0 );
            //#define POP_length_and_string  length=POP_C; string=POP_A
            dtp.PopUl();//��DROP Data�X�^�b�N������o���Ď̂Ă�
            char targByte=(char)dtp.PopUb();
            uint length=dtp.PopUl();
            uint index=dtp.PopUl();
            uint charPos = mp.SearchByteDirectForward(index, length, checked((byte)targByte));
            dtp.PushUl(charPos);
            dtp.PushUl(0);
        }
        private void GyakuItiBaitoKensaku(){   /* ;WORD �t�����Ɉ�o�C�g���� SN  ����:NX */
                /* M�R�[�h=0x01D8 */
            // UCHAR		*strNext;
            // size_t		 length;
            // UCHAR		 targByte;
            // size_t		 charPos;
            // targByte = POP_U_OF_Q;
            // POP_length_and_strNext;
            // charPos = searchByteDirect_Backward( strNext, length, targByte );
            // PUSH_Q( charPos, 0 );
            //#define POP_length_and_strNext length=POP_C; strNext=POP_A+length
            dtp.PopUl();//��DROP Data�X�^�b�N������o���Ď̂Ă�
            char targByte=(char)dtp.PopUb();
            uint length=dtp.PopUl();
            //uint strNext=dtp.PopUl() + length;
            uint index=dtp.PopUl();
            byte[] chars=mp.GetUb(index,length);
            string text = Encoding.GetEncoding("shift_jis").GetString(chars);
            uint charPos = SearchByteDirectBackward( text, length, targByte );
            dtp.PushUl(charPos);
            dtp.PushUl(0);
        }
        private uint SearchByteDirectBackward(string strNext,uint length,char targByte){
            if (strNext.Length==0 || length==0) return 0;
            int pos= strNext.LastIndexOf(targByte);
            return (uint)(pos+1);
        }

    }
}