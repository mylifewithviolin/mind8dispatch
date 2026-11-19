using System.Text;

namespace Mind8Kernel
{
    public partial class Dispatcher
    {
        private void SizeHenko()
        {
            dtp.PopUl();
            uint size = dtp.PopUl();
            dtp.SetUl(size);
        }

        private static int LastCharacterWidth(byte[] content, int length)
        {
            int leads = 0;
            for (int i = length - 2; i >= 0 && IsThisByteKanji(content[i]); i--)
                leads++;
            return leads % 2 == 1 ? 2 : 1;
        }

        private void MigiKiridasi()
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            uint address = (uint)(info >> 32);
            dtp.PopUl();
            uint requested = dtp.PopUl();
            byte[] content = ReadStringBytes(address, length);
            int remaining = content.Length;
            while (remaining > 0 && requested > 0)
            {
                remaining -= LastCharacterWidth(content, remaining);
                requested--;
            }
            uint extracted = checked(length - (uint)remaining);
            ap.SetUl4Ms((uint)remaining, address);
            dtp.PushUl(checked(address + (uint)remaining));
            dtp.PushUl(extracted);
        }

        private void UtanMoji()
        {
            uint length = dtp.PopUl();
            uint address = dtp.PopUl();
            byte[] content = ReadStringBytes(address, length);
            uint value = 0;
            if (content.Length > 0)
            {
                int width = LastCharacterWidth(content, content.Length);
                value = content[^1];
                if (width == 2) value |= (uint)content[^2] << 8;
            }
            dtp.PushUl(value);
            dtp.PushUl(0);
        }

        private void ItiMojiMigiKiridasi()
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            uint address = (uint)(info >> 32);
            byte[] content = ReadStringBytes(address, length);
            uint character = 0;
            if (content.Length > 0)
            {
                int width = LastCharacterWidth(content, content.Length);
                character = content[^1];
                if (width == 2) character |= (uint)content[^2] << 8;
                ap.SetUl4Ms(length - (uint)width, address);
            }
            dtp.PushUl(character);
            dtp.PushUl(0);
        }

        private void KonoBaitoSu()
        {
            uint length = dtp.GetUl();
            dtp.PushUl(length);
            dtp.PushUl(0);
        }

        private void Toridasi()
        {
            dtp.PopUl();
            uint requested = dtp.PopUl();
            uint length = dtp.PopUl();
            uint address = dtp.PopUl();
            byte[] bytes = ReadStringBytes(address, length);
            int position = 0;
            while (position < bytes.Length && requested > 0)
            {
                bool doubleByte = bytes.Length - position >= 2 && IsThisByteKanji(bytes[position]);
                position += doubleByte ? 2 : 1;
                requested--;
            }
            dtp.PushUl(address);
            dtp.PushUl((uint)position);
        }

        private void HitosiiMojiretu()
        {
            uint rightLength = dtp.PopUl();
            uint rightAddress = dtp.PopUl();
            uint leftLength = dtp.PopUl();
            uint leftAddress = dtp.PopUl();
            bool equal = leftLength == rightLength &&
                ReadStringBytes(leftAddress, leftLength).AsSpan().SequenceEqual(ReadStringBytes(rightAddress, rightLength));
            dtp.PushUl(equal ? 1u : 0u);
            dtp.PushUl(0);
        }

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
            uint charPos = 0;
            for (uint position = 0; position < length; position++)
            {
                if (ReadStringBytes(checked(index + position), 1)[0] == checked((byte)targByte))
                {
                    charPos = position + 1;
                    break;
                }
            }
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
            byte[] chars=ReadStringBytes(index,length);
            int position = Array.LastIndexOf(chars, checked((byte)targByte));
            uint charPos = position < 0 ? 0u : (uint)(chars.Length - position);
            dtp.PushUl(charPos);
            dtp.PushUl(0);
        }
    }
}