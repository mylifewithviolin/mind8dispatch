using System.Text;

namespace Mind8Kernel
{
    public partial class Dispatcher
    {
        private uint searchRemainAddress;
        private uint searchRemainLength;

        private void SizeHenko()
        {
            dtp.PopUl();
            uint size = dtp.PopUl();
            dtp.SetUl(size);
        }

        private void BaitoSuWoEru()
        {
            uint length = dtp.GetUl();
            dtp.SetUlOffset(length, 1);
            dtp.SetUl(0);
        }

        private void SizeGensyo()
        {
            dtp.PopUl();
            uint requested = dtp.PopUl();
            uint length = dtp.GetUl();
            dtp.SetUl(length - Math.Min(length, requested));
        }

        private static int LastCharacterWidth(byte[] content, int length)
        {
            int leads = 0;
            for (int i = length - 2; i >= 0 && IsThisByteKanji(content[i]); i--)
                leads++;
            return leads % 2 == 1 ? 2 : 1;
        }

        private static int FirstCharacterWidth(byte[] content) =>
            content.Length >= 2 && IsThisByteKanji(content[0]) ? 2 : 1;
        private static int CharacterWidthAt(byte[] content, int position) =>
            content.Length - position >= 2 && IsThisByteKanji(content[position]) ? 2 : 1;

        private void ItiBaitoSakujyoForJvar() => DeleteJvar(fromRight: false, byCharacter: false);
        private void ItiBaitoSakujyo() => DeleteSvar(fromRight: false, byCharacter: false);
        private void ItiBaitoSakujyoForJinfo() => DeleteJinfo(fromRight: false, byCharacter: false);
        private void ItiMojiSakujyoForJvar() => DeleteJvar(fromRight: false, byCharacter: true);
        private void ItiMojiSakujyo() => DeleteSvar(fromRight: false, byCharacter: true);
        private void ItiMojiSakujyoForJinfo() => DeleteJinfo(fromRight: false, byCharacter: true);
        private void ItiBaitoMigiSakujyoForJvar() => DeleteJvar(fromRight: true, byCharacter: false);
        private void ItiBaitoMigiSakujyo() => DeleteSvar(fromRight: true, byCharacter: false);
        private void ItiBaitoMigiSakujyoForJinfo() => DeleteJinfo(fromRight: true, byCharacter: false);
        private void ItiMojiMigiSakujyoForJvar() => DeleteJvar(fromRight: true, byCharacter: true);
        private void ItiMojiMigiSakujyo() => DeleteSvar(fromRight: true, byCharacter: true);
        private void ItiMojiMigiSakujyoForJinfo() => DeleteJinfo(fromRight: true, byCharacter: true);
        private void IsSvarEmpty() => PushQuadBoolean(dtp.GetUlOffset(0) == 0);
        private void IsSvarNotEmpty() => PushQuadBoolean(dtp.GetUlOffset(0) != 0);
        private void SvarDeleteFirstByte() => DeleteSvarStack(fromRight: false, byCharacter: false);
        private void SvarDeleteLastByte() => DeleteSvarStack(fromRight: true, byCharacter: false);
        private void SvarDeleteFirstCharacter() => DeleteSvarStack(fromRight: false, byCharacter: true);
        private void SvarDeleteLastCharacter() => DeleteSvarStack(fromRight: true, byCharacter: true);
        private void SvarCutFirstByte() => CutSvarStack(fromRight: false, byCharacter: false);
        private void SvarCutLastByte() => CutSvarStack(fromRight: true, byCharacter: false);
        private void SvarCutFirstCharacter() => CutSvarStack(fromRight: false, byCharacter: true);
        private void SvarCutLastCharacter() => CutSvarStack(fromRight: true, byCharacter: true);
        private void SvarCutBytesFromStart() => CutSvarByteRangeStack(fromRight: false);
        private void SvarCutBytesFromEnd() => CutSvarByteRangeStack(fromRight: true);
        private void SvarDeleteBytesFromStart() => DeleteSvarByteRangeStack(fromRight: false);
        private void SvarDeleteBytesFromEnd() => DeleteSvarByteRangeStack(fromRight: true);

        private void PushQuadBoolean(bool value)
        {
            dtp.PushUl(value ? 1u : 0u);
            dtp.PushUl(0);
        }

        private void DeleteSvarStack(bool fromRight, bool byCharacter)
        {
            uint length = dtp.GetUlOffset(0);
            if (length == 0) return;

            uint address = dtp.GetUlOffset(1);
            uint width = 1;
            if (byCharacter)
            {
                byte[] content = ReadStringBytes(address, length);
                width = (uint)(fromRight ? LastCharacterWidth(content, content.Length) : FirstCharacterWidth(content));
            }

            dtp.SetUlOffset(length - width, 0);
            if (!fromRight)
                dtp.SetUlOffset(AddAddress(address, width), 1);
        }

        private void CutSvarStack(bool fromRight, bool byCharacter)
        {
            uint length = dtp.GetUlOffset(0);
            if (length == 0)
            {
                dtp.PushUl(0);
                dtp.PushUl(0);
                return;
            }

            uint address = dtp.GetUlOffset(1);
            byte[] content = ReadStringBytes(address, length);
            uint value = GetCutCharacter(content, fromRight, byCharacter, out int width);
            dtp.SetUlOffset(length - (uint)width, 0);
            if (!fromRight)
                dtp.SetUlOffset(AddAddress(address, (uint)width), 1);
            dtp.PushUl(value);
            dtp.PushUl(0);
        }

        private void CutSvarByteRangeStack(bool fromRight)
        {
            uint requested = PopRequestedByteCount();
            uint length = dtp.GetUlOffset(0);
            uint address = dtp.GetUlOffset(1);
            uint bytes = Math.Min(length, requested);
            uint resultAddress = fromRight ? AddAddress(address, length - bytes) : address;
            dtp.SetUlOffset(length - bytes, 0);
            if (!fromRight)
                dtp.SetUlOffset(AddAddress(address, bytes), 1);
            dtp.PushUl(resultAddress);
            dtp.PushUl(bytes);
        }

        private void DeleteSvarByteRangeStack(bool fromRight)
        {
            uint requested = PopRequestedByteCount();
            uint length = dtp.GetUlOffset(0);
            uint bytes = Math.Min(length, requested);
            dtp.SetUlOffset(length - bytes, 0);
            if (!fromRight)
                dtp.SetUlOffset(AddAddress(dtp.GetUlOffset(1), bytes), 1);
        }

        private void DeleteJvar(bool fromRight, bool byCharacter) =>
            DeleteStringRecord(AccessAddress, fromRight, byCharacter);

        private void DeleteJinfo(bool fromRight, bool byCharacter)
        {
            (uint address, _) = ReadJinfo();
            DeleteStringRecord(address, fromRight, byCharacter);
        }

        private void DeleteStringRecord(uint address, bool fromRight, bool byCharacter)
        {
            uint length = ReadMemoryWord(address);
            if (length == 0) return;

            uint margin = ReadMemoryWord(AddAddress(address, 4));
            int width = 1;
            if (byCharacter)
            {
                uint stringAddress = AddAddress(address, checked(8 + margin));
                byte[] content = ReadStringBytes(stringAddress, length);
                width = fromRight ? LastCharacterWidth(content, content.Length) : FirstCharacterWidth(content);
            }

            WriteMemoryWord(address, length - (uint)width);
            if (!fromRight)
                WriteMemoryWord(AddAddress(address, 4), checked(margin + (uint)width));
        }

        private void DeleteSvar(bool fromRight, bool byCharacter)
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            if (length == 0) return;

            uint address = (uint)(info >> 32);
            int width = 1;
            if (byCharacter)
            {
                byte[] content = ReadStringBytes(address, length);
                width = fromRight ? LastCharacterWidth(content, content.Length) : FirstCharacterWidth(content);
            }

            ap.SetUl4Ms(length - (uint)width, fromRight ? address : AddAddress(address, (uint)width));
        }

        private void ItiBaitoKiridasiForJvar() => CutJvar(fromRight: false, byCharacter: false);
        private void ItiBaitoKiridasi() => CutSvar(fromRight: false, byCharacter: false);
        private void ItiBaitoKiridasiForJinfo() => CutJinfo(fromRight: false, byCharacter: false);
        private void ItiMojiKiridasiForJvar() => CutJvar(fromRight: false, byCharacter: true);
        private void ItiMojiKiridasi() => CutSvar(fromRight: false, byCharacter: true);
        private void ItiMojiKiridasiForJinfo() => CutJinfo(fromRight: false, byCharacter: true);
        private void ItiBaitoMigiKiridasiForJvar() => CutJvar(fromRight: true, byCharacter: false);
        private void ItiBaitoMigiKiridasi() => CutSvar(fromRight: true, byCharacter: false);
        private void ItiBaitoMigiKiridasiForJinfo() => CutJinfo(fromRight: true, byCharacter: false);
        private void ItiMojiMigiKiridasiForJvar() => CutJvar(fromRight: true, byCharacter: true);
        private void ItiMojiMigiKiridasiForJinfo() => CutJinfo(fromRight: true, byCharacter: true);

        private void CutJvar(bool fromRight, bool byCharacter) =>
            CutStringRecord(AccessAddress, fromRight, byCharacter);

        private void CutJinfo(bool fromRight, bool byCharacter)
        {
            (uint address, _) = ReadJinfo();
            CutStringRecord(address, fromRight, byCharacter);
        }

        private void CutStringRecord(uint address, bool fromRight, bool byCharacter)
        {
            uint length = ReadMemoryWord(address);
            if (length == 0)
            {
                dtp.PushUl(0);
                dtp.PushUl(0);
                return;
            }

            uint margin = ReadMemoryWord(AddAddress(address, 4));
            uint stringAddress = AddAddress(address, checked(8 + margin));
            byte[] content = ReadStringBytes(stringAddress, length);
            uint value = GetCutCharacter(content, fromRight, byCharacter, out int width);
            dtp.PushUl(value);
            dtp.PushUl(0);

            WriteMemoryWord(address, length - (uint)width);
            if (!fromRight)
                WriteMemoryWord(AddAddress(address, 4), checked(margin + (uint)width));
        }

        private void CutSvar(bool fromRight, bool byCharacter)
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            if (length == 0)
            {
                dtp.PushUl(0);
                dtp.PushUl(0);
                return;
            }

            uint address = (uint)(info >> 32);
            byte[] content = ReadStringBytes(address, length);
            uint value = GetCutCharacter(content, fromRight, byCharacter, out int width);
            dtp.PushUl(value);
            dtp.PushUl(0);
            ap.SetUl4Ms(length - (uint)width, fromRight ? address : AddAddress(address, (uint)width));
        }

        private static uint GetCutCharacter(byte[] content, bool fromRight, bool byCharacter, out int width)
        {
            if (!byCharacter)
            {
                width = 1;
                return fromRight ? content[^1] : content[0];
            }

            width = fromRight ? LastCharacterWidth(content, content.Length) : FirstCharacterWidth(content);
            if (fromRight)
                return (uint)(content[^1] | (width == 2 ? content[^2] << 8 : 0));
            return (uint)(content[0] << (width == 2 ? 8 : 0) | (width == 2 ? content[1] : 0));
        }

        private void BaitoRetuKiridasiForJvar() => CutByteRangeJvar(fromRight: false);
        private void BaitoRetuKiridasi() => CutByteRangeSvar(fromRight: false);
        private void BaitoRetuKiridasiForJinfo() => CutByteRangeJinfo(fromRight: false);
        private void BaitoRetuMigiKiridasiForJvar() => CutByteRangeJvar(fromRight: true);
        private void BaitoRetuMigiKiridasi() => CutByteRangeSvar(fromRight: true);
        private void BaitoRetuMigiKiridasiForJinfo() => CutByteRangeJinfo(fromRight: true);
        private void BaitoRetuSakujyoForJvar() => DeleteByteRangeJvar(fromRight: false);
        private void BaitoRetuSakujyo() => DeleteByteRangeSvar(fromRight: false);
        private void BaitoRetuSakujyoForJinfo() => DeleteByteRangeJinfo(fromRight: false);
        private void BaitoRetuMigiSakujyoForJvar() => DeleteByteRangeJvar(fromRight: true);
        private void BaitoRetuMigiSakujyo() => DeleteByteRangeSvar(fromRight: true);
        private void BaitoRetuMigiSakujyoForJinfo() => DeleteByteRangeJinfo(fromRight: true);

        private uint PopRequestedByteCount()
        {
            dtp.PopUl();
            return dtp.PopUl();
        }

        private void CutByteRangeJvar(bool fromRight)
        {
            uint address = AccessAddress;
            uint requested = PopRequestedByteCount();
            CutByteRangeRecord(address, fromRight, requested);
        }

        private void CutByteRangeJinfo(bool fromRight)
        {
            (uint address, _) = ReadJinfo();
            uint requested = PopRequestedByteCount();
            CutByteRangeRecord(address, fromRight, requested);
        }

        private void CutByteRangeSvar(bool fromRight)
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            uint address = (uint)(info >> 32);
            uint requested = PopRequestedByteCount();
            uint bytes = Math.Min(length, requested);
            uint resultAddress = fromRight ? AddAddress(address, length - bytes) : address;
            dtp.PushUl(resultAddress);
            dtp.PushUl(bytes);
            ap.SetUl4Ms(length - bytes, fromRight ? address : AddAddress(address, bytes));
        }

        private void CutByteRangeRecord(uint address, bool fromRight, uint requested)
        {
            uint length = ReadMemoryWord(address);
            uint margin = ReadMemoryWord(AddAddress(address, 4));
            uint stringAddress = AddAddress(address, checked(8 + margin));
            uint bytes = Math.Min(length, requested);
            uint resultAddress = fromRight ? AddAddress(stringAddress, length - bytes) : stringAddress;
            dtp.PushUl(resultAddress);
            dtp.PushUl(bytes);

            WriteMemoryWord(address, length - bytes);
            if (!fromRight)
                WriteMemoryWord(AddAddress(address, 4), checked(margin + bytes));
        }

        private void DeleteByteRangeJvar(bool fromRight)
        {
            uint requested = PopRequestedByteCount();
            DeleteByteRangeRecord(AccessAddress, fromRight, requested);
        }

        private void DeleteByteRangeJinfo(bool fromRight)
        {
            uint requested = PopRequestedByteCount();
            (uint address, _) = ReadJinfo();
            DeleteByteRangeRecord(address, fromRight, requested);
        }

        private void DeleteByteRangeSvar(bool fromRight)
        {
            uint requested = PopRequestedByteCount();
            ulong info = ap.GetUll();
            uint length = (uint)info;
            uint address = (uint)(info >> 32);
            uint bytes = Math.Min(length, requested);
            ap.SetUl4Ms(length - bytes, fromRight ? address : AddAddress(address, bytes));
        }

        private void DeleteByteRangeRecord(uint address, bool fromRight, uint requested)
        {
            uint length = ReadMemoryWord(address);
            uint bytes = Math.Min(length, requested);
            WriteMemoryWord(address, length - bytes);
            if (!fromRight)
                WriteMemoryWord(AddAddress(address, 4), checked(ReadMemoryWord(AddAddress(address, 4)) + bytes));
        }

        private void KiridasiForJvar() => CutCharacterRangeJvar(fromRight: false);
        private void Kiridasi() => CutCharacterRangeSvar(fromRight: false);
        private void KiridasiForJinfo() => CutCharacterRangeJinfo(fromRight: false);
        private void MigiKiridasiForJvar() => CutCharacterRangeJvar(fromRight: true);
        private void MigiKiridasiForJinfo() => CutCharacterRangeJinfo(fromRight: true);
        private void MigiKiridasi() => CutCharacterRangeSvar(fromRight: true);

        private void BundanForJvar() => SplitStringPositionJvar(byCharacter: true);
        private void Bundan() => SplitStringPositionSvar(byCharacter: true);
        private void BundanForJinfo() => SplitStringPositionJinfo(byCharacter: true);
        private void ByteBundanForJvar() => SplitStringPositionJvar(byCharacter: false);
        private void ByteBundan() => SplitStringPositionSvar(byCharacter: false);
        private void ByteBundanForJinfo() => SplitStringPositionJinfo(byCharacter: false);
        private void SakujyoForJvar() => DeleteCharacterRangeJvar(fromRight: false);
        private void Sakujyo() => DeleteCharacterRangeSvar(fromRight: false);
        private void SakujyoForJinfo() => DeleteCharacterRangeJinfo(fromRight: false);
        private void MigiSakujyoForJvar() => DeleteCharacterRangeJvar(fromRight: true);
        private void MigiSakujyo() => DeleteCharacterRangeSvar(fromRight: true);
        private void MigiSakujyoForJinfo() => DeleteCharacterRangeJinfo(fromRight: true);
        private void KuhakuWoNozokuForJvar() => TrimWhitespaceJvar(fromRight: false);
        private void KuhakuWoNozoku() => TrimWhitespaceSvar(fromRight: false);
        private void KuhakuWoNozokuForJinfo() => TrimWhitespaceJinfo(fromRight: false);
        private void MigiKuhakuWoNozokuForJvar() => TrimWhitespaceJvar(fromRight: true);
        private void MigiKuhakuWoNozoku() => TrimWhitespaceSvar(fromRight: true);
        private void MigiKuhakuWoNozokuForJinfo() => TrimWhitespaceJinfo(fromRight: true);
        private void TangoKiridasiForJvar() => ExtractWordJvar();
        private void TangoKiridasi() => ExtractWordSvar();
        private void TangoKiridasiForJinfo() => ExtractWordJinfo();
        private void Mojisu() => CountStringCharacters();
        private void SatanBaito() => PushStringCharacter(fromRight: false, byCharacter: false);
        private void SatanMoji() => PushStringCharacter(fromRight: false, byCharacter: true);
        private void UtanBaito() => PushStringCharacter(fromRight: true, byCharacter: false);
        private void SiteiItiNoMoji() => PushCharacterAt(fromRight: false);
        private void MigiSiteiItiNoMoji() => PushCharacterAt(fromRight: true);
        private void ItiMojiKensaku() => SearchCharacter();
        private void Kensaku() => SearchStringByCharacter();
        private void ZanKensakumojiretu()
        {
            dtp.PushUl(searchRemainAddress);
            dtp.PushUl(searchRemainLength);
        }
        private void BaitoRetuKensaku() => SearchByteString();
        private void MojiretuHikaku0() => CompareStringBytes();
        private void MojiretuHikaku() => CompareStringCharacters(ignoreAsciiCase: false);
        private void FilenameHikaku() => CompareStringCharacters(ignoreAsciiCase: true);
        private void IsKanjiDaiitiBaitoSjis() =>
            UpdateQuadInteger(value => IsThisByteKanji(unchecked((byte)value)) ? 1u : 0u);
        private void IsKanjiDaiitiBaitoEuc() =>
            UpdateQuadInteger(value => (value & 0x80) != 0 ? 1u : 0u);
        private void IsKanjiDaiitiBaito() =>
            UpdateQuadInteger(value => IsThisByteKanji(unchecked((byte)value)) ? 1u : 0u);
        private void IsKanjiCode() => UpdateQuadInteger(value => (ushort)value > byte.MaxValue ? 1u : 0u);
        private void IsHankakuKana() =>
            UpdateQuadInteger(value => (ushort)value is >= 0xA1 and <= 0xDF ? 1u : 0u);
        private void MojiHaba() => UpdateQuadInteger(value => (ushort)value > byte.MaxValue ? 2u : 1u);
        private void HyojiHaba() => UpdateQuadInteger(value => (ushort)value > byte.MaxValue ? 2u : 1u);
        private void IsKuhaku() => UpdateQuadInteger(value =>
        {
            ushort character = (ushort)value;
            return character > byte.MaxValue
                ? (IsWhitespaceKanji(character) ? 1u : 0u)
                : (IsWhitespaceAscii((byte)character) ? 1u : 0u);
        });
        private void CSjisToJis() => UpdateQuadInteger(ConvertSjisToJis);
        private void CJisToSjis() => UpdateQuadInteger(ConvertJisToSjis);
        private void ItiMojiOhmojiHenkan() => UpdateQuadInteger(value => ConvertCharacterToUpper((ushort)value));
        private void ItiMojiSutiHenkan() => ConvertCharacterToNumber(hexadecimal: false);
        private void ItiMojiSutiHenkanHex() => ConvertCharacterToNumber(hexadecimal: true);
        private void ItiMojiZenkakuHenkan() => UpdateQuadInteger(value => ConvertCharacterToFullWidth((ushort)value));
        private void IsKuhakuMatahaSeparator() => UpdateQuadInteger(value =>
        {
            ushort character = (ushort)value;
            if (character > byte.MaxValue)
                return IsWhitespaceKanji(character) || character is 0x8169 or 0x816A or 0x816D or 0x816E or 0x8142 ? 1u : 0u;
            return IsWhitespaceAscii((byte)character) || character is 0x28 or 0x29 or 0x5B or 0x5D or 0xA1 ? 1u : 0u;
        });

        private void UpdateQuadInteger(Func<uint, uint> update)
        {
            uint value = dtp.GetUlOffset(1);
            dtp.SetUlOffset(update(value), 1);
        }

        private static uint ConvertSjisToJis(uint value)
        {
            ushort character = (ushort)value;
            if (character <= byte.MaxValue) return character;

            byte high = (byte)(character >> 8);
            byte low = (byte)character;
            if (high >= 0xE0)
                high = unchecked((byte)(high - 0x40));
            high = unchecked((byte)(high - 0x70));
            if ((low & 0x80) != 0)
                low--;
            high = unchecked((byte)(high << 1));
            if (low >= 0x9E)
                low = unchecked((byte)(low - 0x5E));
            else
                high--;
            low = unchecked((byte)(low - 0x1F));
            return (uint)(high << 8 | low);
        }

        private static uint ConvertJisToSjis(uint value)
        {
            ushort character = (ushort)value;
            if (character <= byte.MaxValue) return character;

            byte high = (byte)(character >> 8);
            byte low = (byte)character;
            low = unchecked((byte)(low + 0x1F));
            if ((high & 1) == 0)
            {
                high >>= 1;
                high--;
                low = unchecked((byte)(low + 0x5E));
            }
            else
            {
                high >>= 1;
            }

            if (low >= 0x7F)
                low++;
            high = unchecked((byte)(high + 0x71));
            if (high >= 0xA0)
                high = unchecked((byte)(high + 0x40));
            return (uint)(high << 8 | low);
        }

        private static ushort ConvertCharacterToUpper(ushort character)
        {
            if (character is >= (ushort)'a' and <= (ushort)'z')
                return (ushort)(character - ('a' - 'A'));
            if (character is >= 0x8281 and <= 0x829A)
                return (ushort)(character - 0x21);
            return character;
        }

        private static ushort ConvertCharacterToFullWidth(ushort character)
        {
            if (character > byte.MaxValue) return character;
            if (character is >= 0x20 and <= 0x7E)
                return FullWidthAsciiTable[character - 0x20];
            if (character is >= 0xA0 and <= 0xDF)
                return FullWidthKanaTable[character - 0xA0];
            return character;
        }

        private static readonly ushort[] FullWidthAsciiTable =
        {
            0x8140, 0x8149, 0x8168, 0x8194, 0x8190, 0x8193, 0x8195, 0x8166,
            0x8169, 0x816A, 0x8196, 0x817B, 0x8143, 0x817C, 0x8144, 0x815E,
            0x824F, 0x8250, 0x8251, 0x8252, 0x8253, 0x8254, 0x8255, 0x8256,
            0x8257, 0x8258, 0x8146, 0x8147, 0x8183, 0x8181, 0x8184, 0x8148,
            0x8197, 0x8260, 0x8261, 0x8262, 0x8263, 0x8264, 0x8265, 0x8266,
            0x8267, 0x8268, 0x8269, 0x826A, 0x826B, 0x826C, 0x826D, 0x826E,
            0x826F, 0x8270, 0x8271, 0x8272, 0x8273, 0x8274, 0x8275, 0x8276,
            0x8277, 0x8278, 0x8279, 0x816D, 0x818F, 0x816E, 0x814F, 0x8151,
            0x8140, 0x8281, 0x8282, 0x8283, 0x8284, 0x8285, 0x8286, 0x8287,
            0x8288, 0x8289, 0x828A, 0x828B, 0x828C, 0x828D, 0x828E, 0x828F,
            0x8290, 0x8291, 0x8292, 0x8293, 0x8294, 0x8295, 0x8296, 0x8297,
            0x8298, 0x8299, 0x829A, 0x816F, 0x8162, 0x8170, 0x8150
        };

        private static readonly ushort[] FullWidthKanaTable =
        {
            0x8140, 0x8142, 0x8175, 0x8176, 0x8141, 0x8145, 0x8392, 0x8340,
            0x8342, 0x8344, 0x8346, 0x8348, 0x8383, 0x8385, 0x8387, 0x8362,
            0x815B, 0x8341, 0x8343, 0x8345, 0x8347, 0x8349, 0x834A, 0x834C,
            0x834E, 0x8350, 0x8352, 0x8354, 0x8356, 0x8358, 0x835A, 0x835C,
            0x835E, 0x8360, 0x8363, 0x8365, 0x8367, 0x8369, 0x836A, 0x836B,
            0x836C, 0x836D, 0x836E, 0x8371, 0x8374, 0x8377, 0x837A, 0x837D,
            0x837E, 0x8380, 0x8381, 0x8382, 0x8384, 0x8386, 0x8388, 0x8389,
            0x838A, 0x838B, 0x838C, 0x838D, 0x838F, 0x8393, 0x814A, 0x814B
        };

        private void ConvertCharacterToNumber(bool hexadecimal)
        {
            ushort character = (ushort)dtp.GetUlOffset(1);
            int value = GetCharacterNumericValue(character, hexadecimal);
            dtp.SetUlOffset(value < 0 ? 0u : (uint)value, 1);
            dtp.PushUl(value < 0 ? 0u : 1u);
            dtp.PushUl(0);
        }

        private static int GetCharacterNumericValue(ushort character, bool hexadecimal)
        {
            int digit = character switch
            {
                >= 0x30 and <= 0x39 => character - 0x30,
                >= 0x824F and <= 0x8258 => character - 0x824F,
                _ => -1
            };
            if (digit >= 0 || !hexadecimal) return digit;

            return character switch
            {
                >= 0x41 and <= 0x46 => character - 0x41 + 10,
                >= 0x61 and <= 0x66 => character - 0x61 + 10,
                >= 0x8260 and <= 0x8265 => character - 0x8260 + 10,
                >= 0x8281 and <= 0x8286 => character - 0x8281 + 10,
                _ => -1
            };
        }

        private static (uint prefixBytes, uint removedBytes) GetSplitByteCounts(
            byte[] content, uint requestedPosition, bool byCharacter)
        {
            if (requestedPosition == 0 || content.Length == 0)
                return (0, 0);

            uint prefixBytes = byCharacter
                ? CountCharacterBytes(content, requestedPosition - 1, fromRight: false)
                : Math.Min((uint)content.Length, requestedPosition - 1);
            int position = checked((int)prefixBytes);
            uint delimiterBytes = position < content.Length
                ? (uint)(byCharacter ? CharacterWidthAt(content, position) : 1)
                : 0;
            return (prefixBytes, checked(prefixBytes + delimiterBytes));
        }

        private void SplitStringPositionJvar(bool byCharacter)
        {
            uint address = AccessAddress;
            uint requestedPosition = PopRequestedByteCount();
            SplitStringPositionRecord(address, requestedPosition, byCharacter);
        }

        private void SplitStringPositionJinfo(bool byCharacter)
        {
            (uint address, _) = ReadJinfo();
            uint requestedPosition = PopRequestedByteCount();
            SplitStringPositionRecord(address, requestedPosition, byCharacter);
        }

        private void SplitStringPositionSvar(bool byCharacter)
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            uint address = (uint)(info >> 32);
            uint requestedPosition = PopRequestedByteCount();
            byte[] content = ReadStringBytes(address, length);
            (uint prefixBytes, uint removedBytes) =
                GetSplitByteCounts(content, requestedPosition, byCharacter);
            dtp.PushUl(address);
            dtp.PushUl(prefixBytes);
            ap.SetUl4Ms(length - removedBytes, AddAddress(address, removedBytes));
        }

        private void SplitStringPositionRecord(uint address, uint requestedPosition, bool byCharacter)
        {
            uint length = ReadMemoryWord(address);
            uint margin = ReadMemoryWord(AddAddress(address, 4));
            uint stringAddress = AddAddress(address, checked(8 + margin));
            byte[] content = ReadStringBytes(stringAddress, length);
            (uint prefixBytes, uint removedBytes) =
                GetSplitByteCounts(content, requestedPosition, byCharacter);
            dtp.PushUl(stringAddress);
            dtp.PushUl(prefixBytes);

            WriteMemoryWord(address, length - removedBytes);
            WriteMemoryWord(AddAddress(address, 4), checked(margin + removedBytes));
        }

        private void DeleteCharacterRangeJvar(bool fromRight)
        {
            uint address = AccessAddress;
            uint requested = PopRequestedByteCount();
            DeleteCharacterRangeRecord(address, fromRight, requested);
        }

        private void DeleteCharacterRangeJinfo(bool fromRight)
        {
            (uint address, _) = ReadJinfo();
            uint requested = PopRequestedByteCount();
            DeleteCharacterRangeRecord(address, fromRight, requested);
        }

        private void DeleteCharacterRangeSvar(bool fromRight)
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            uint address = (uint)(info >> 32);
            uint requested = PopRequestedByteCount();
            byte[] content = ReadStringBytes(address, length);
            uint bytes = CountCharacterBytes(content, requested, fromRight);
            ap.SetUl4Ms(length - bytes, fromRight ? address : AddAddress(address, bytes));
        }

        private void DeleteCharacterRangeRecord(uint address, bool fromRight, uint requested)
        {
            uint length = ReadMemoryWord(address);
            uint margin = ReadMemoryWord(AddAddress(address, 4));
            uint stringAddress = AddAddress(address, checked(8 + margin));
            byte[] content = ReadStringBytes(stringAddress, length);
            uint bytes = CountCharacterBytes(content, requested, fromRight);

            WriteMemoryWord(address, length - bytes);
            if (!fromRight)
                WriteMemoryWord(AddAddress(address, 4), checked(margin + bytes));
        }

        private void TrimWhitespaceJvar(bool fromRight)
        {
            uint address = AccessAddress;
            TrimWhitespaceRecord(address, fromRight);
        }

        private void TrimWhitespaceJinfo(bool fromRight)
        {
            (uint address, _) = ReadJinfo();
            TrimWhitespaceRecord(address, fromRight);
        }

        private void TrimWhitespaceSvar(bool fromRight)
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            uint address = (uint)(info >> 32);
            byte[] content = ReadStringBytes(address, length);
            uint bytes = CountWhitespaceBytes(content, fromRight);
            ap.SetUl4Ms(length - bytes, fromRight ? address : AddAddress(address, bytes));
        }

        private void TrimWhitespaceRecord(uint address, bool fromRight)
        {
            uint length = ReadMemoryWord(address);
            uint margin = ReadMemoryWord(AddAddress(address, 4));
            uint stringAddress = AddAddress(address, checked(8 + margin));
            byte[] content = ReadStringBytes(stringAddress, length);
            uint bytes = CountWhitespaceBytes(content, fromRight);

            WriteMemoryWord(address, length - bytes);
            if (!fromRight)
                WriteMemoryWord(AddAddress(address, 4), checked(margin + bytes));
        }

        private static uint CountWhitespaceBytes(byte[] content, bool fromRight)
        {
            if (!fromRight)
            {
                int position = 0;
                while (position < content.Length)
                {
                    if (content.Length - position >= 2 && IsThisByteKanji(content[position]))
                    {
                        ushort character = (ushort)(content[position] << 8 | content[position + 1]);
                        if (!IsWhitespaceKanji(character)) break;
                        position += 2;
                    }
                    else
                    {
                        if (!IsWhitespaceAscii(content[position])) break;
                        position++;
                    }
                }
                return (uint)position;
            }

            int remaining = content.Length;
            while (remaining > 0)
            {
                int width = LastCharacterWidth(content, remaining);
                if (width == 2)
                {
                    ushort character = (ushort)(content[remaining - 2] << 8 | content[remaining - 1]);
                    if (!IsWhitespaceKanji(character)) break;
                }
                else if (!IsWhitespaceAscii(content[remaining - 1]))
                {
                    break;
                }
                remaining -= width;
            }
            return (uint)(content.Length - remaining);
        }

        private static bool IsWhitespaceAscii(byte value) => value <= 0x20 || value == 0x2C;

        private static bool IsWhitespaceKanji(ushort value) =>
            value is 0x8140 or 0x8141 or 0x8143;

        private static uint CountWordBytes(byte[] content, int position)
        {
            int start = position;
            while (position < content.Length)
            {
                if (content.Length - position >= 2 && IsThisByteKanji(content[position]))
                {
                    ushort character = (ushort)(content[position] << 8 | content[position + 1]);
                    if (IsWhitespaceKanji(character)) break;
                    position += 2;
                }
                else
                {
                    if (IsWhitespaceAscii(content[position])) break;
                    position++;
                }
            }
            return (uint)(position - start);
        }

        private static (uint leadingWhitespace, uint wordBytes) GetWordRange(byte[] content)
        {
            uint leadingWhitespace = CountWhitespaceBytes(content, fromRight: false);
            uint wordBytes = CountWordBytes(content, checked((int)leadingWhitespace));
            return (leadingWhitespace, wordBytes);
        }

        private void ExtractWordJvar()
        {
            uint address = AccessAddress;
            ExtractWordRecord(address);
        }

        private void ExtractWordJinfo()
        {
            (uint address, _) = ReadJinfo();
            ExtractWordRecord(address);
        }

        private void ExtractWordSvar()
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            uint address = (uint)(info >> 32);
            byte[] content = ReadStringBytes(address, length);
            (uint leadingWhitespace, uint wordBytes) = GetWordRange(content);
            uint removedBytes = checked(leadingWhitespace + wordBytes);
            uint wordAddress = AddAddress(address, leadingWhitespace);
            dtp.PushUl(wordAddress);
            dtp.PushUl(wordBytes);
            ap.SetUl4Ms(length - removedBytes, AddAddress(address, removedBytes));
        }

        private void ExtractWordRecord(uint address)
        {
            uint length = ReadMemoryWord(address);
            uint margin = ReadMemoryWord(AddAddress(address, 4));
            uint stringAddress = AddAddress(address, checked(8 + margin));
            byte[] content = ReadStringBytes(stringAddress, length);
            (uint leadingWhitespace, uint wordBytes) = GetWordRange(content);
            uint removedBytes = checked(leadingWhitespace + wordBytes);
            uint wordAddress = AddAddress(stringAddress, leadingWhitespace);
            dtp.PushUl(wordAddress);
            dtp.PushUl(wordBytes);

            WriteMemoryWord(address, length - removedBytes);
            WriteMemoryWord(AddAddress(address, 4), checked(margin + removedBytes));
        }

        private static uint CountCharacterBytes(byte[] content, uint requested, bool fromRight)
        {
            if (!fromRight)
            {
                int position = 0;
                while (position < content.Length && requested > 0)
                {
                    position += content.Length - position >= 2 && IsThisByteKanji(content[position]) ? 2 : 1;
                    requested--;
                }
                return (uint)position;
            }

            int remaining = content.Length;
            while (remaining > 0 && requested > 0)
            {
                remaining -= LastCharacterWidth(content, remaining);
                requested--;
            }
            return (uint)(content.Length - remaining);
        }

        private void CountStringCharacters()
        {
            uint length = dtp.PopUl();
            uint address = dtp.PopUl();
            byte[] content = ReadStringBytes(address, length);
            uint count = 0;
            for (int position = 0; position < content.Length; count++)
                position += content.Length - position >= 2 && IsThisByteKanji(content[position]) ? 2 : 1;
            dtp.PushUl(count);
            dtp.PushUl(0);
        }

        private void PushStringCharacter(bool fromRight, bool byCharacter)
        {
            uint length = dtp.PopUl();
            uint address = dtp.PopUl();
            byte[] content = ReadStringBytes(address, length);
            uint value = content.Length == 0 ? 0 : GetCutCharacter(content, fromRight, byCharacter, out _);
            dtp.PushUl(value);
            dtp.PushUl(0);
        }

        private void PushCharacterAt(bool fromRight)
        {
            uint position = PopRequestedByteCount();
            uint length = dtp.PopUl();
            uint address = dtp.PopUl();
            byte[] content = ReadStringBytes(address, length);
            uint value = 0;
            if (position > 0 && content.Length > 0)
            {
                uint skippedBytes = CountCharacterBytes(content, position - 1, fromRight);
                int remainingLength = content.Length - checked((int)skippedBytes);
                if (remainingLength > 0)
                {
                    byte[] remaining = fromRight
                        ? content.AsSpan(0, remainingLength).ToArray()
                        : content.AsSpan(checked((int)skippedBytes)).ToArray();
                    value = GetCutCharacter(remaining, fromRight, byCharacter: true, out _);
                }
            }
            dtp.PushUl(value);
            dtp.PushUl(0);
        }

        private void CutCharacterRangeJvar(bool fromRight)
        {
            uint address = AccessAddress;
            uint requested = PopRequestedByteCount();
            CutCharacterRangeRecord(address, fromRight, requested);
        }

        private void CutCharacterRangeJinfo(bool fromRight)
        {
            (uint address, _) = ReadJinfo();
            uint requested = PopRequestedByteCount();
            CutCharacterRangeRecord(address, fromRight, requested);
        }

        private void CutCharacterRangeSvar(bool fromRight)
        {
            ulong info = ap.GetUll();
            uint length = (uint)info;
            uint address = (uint)(info >> 32);
            uint requested = PopRequestedByteCount();
            byte[] content = ReadStringBytes(address, length);
            uint bytes = CountCharacterBytes(content, requested, fromRight);
            uint resultAddress = fromRight ? AddAddress(address, length - bytes) : address;
            dtp.PushUl(resultAddress);
            dtp.PushUl(bytes);
            ap.SetUl4Ms(length - bytes, fromRight ? address : AddAddress(address, bytes));
        }

        private void CutCharacterRangeRecord(uint address, bool fromRight, uint requested)
        {
            uint length = ReadMemoryWord(address);
            uint margin = ReadMemoryWord(AddAddress(address, 4));
            uint stringAddress = AddAddress(address, checked(8 + margin));
            byte[] content = ReadStringBytes(stringAddress, length);
            uint bytes = CountCharacterBytes(content, requested, fromRight);
            uint resultAddress = fromRight ? AddAddress(stringAddress, length - bytes) : stringAddress;
            dtp.PushUl(resultAddress);
            dtp.PushUl(bytes);

            WriteMemoryWord(address, length - bytes);
            if (!fromRight)
                WriteMemoryWord(AddAddress(address, 4), checked(margin + bytes));
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

        private void MigiToridasi()
        {
            dtp.PopUl();
            uint requested = dtp.PopUl();
            uint length = dtp.PopUl();
            uint address = dtp.PopUl();
            byte[] content = ReadStringBytes(address, length);
            uint bytes = CountCharacterBytes(content, requested, fromRight: true);
            dtp.PushUl(AddAddress(address, length - bytes));
            dtp.PushUl(bytes);
        }

        private void SearchCharacter()
        {
            dtp.PopUl();
            ushort target = (ushort)dtp.PopUl();
            uint length = dtp.PopUl();
            uint address = dtp.PopUl();
            byte[] content = ReadStringBytes(address, length);
            uint characterPosition = 1;
            for (int position = 0; position < content.Length; characterPosition++)
            {
                int width = CharacterWidthAt(content, position);
                ushort character = (ushort)(width == 2
                    ? content[position] << 8 | content[position + 1]
                    : content[position]);
                if ((target > byte.MaxValue && width == 2 || target <= byte.MaxValue && width == 1)
                    && character == target)
                    break;
                position += width;
            }

            if (characterPosition > CountStringCharacterCount(content))
                characterPosition = 0;
            dtp.PushUl(characterPosition);
            dtp.PushUl(0);
        }

        private static uint CountStringCharacterCount(byte[] content)
        {
            uint count = 0;
            for (int position = 0; position < content.Length; count++)
                position += CharacterWidthAt(content, position);
            return count;
        }

        private void SearchStringByCharacter()
        {
            uint keyLength = dtp.PopUl();
            uint keyAddress = dtp.PopUl();
            uint orgLength = dtp.PopUl();
            uint orgAddress = dtp.PopUl();
            byte[] org = ReadStringBytes(orgAddress, orgLength);
            byte[] key = ReadStringBytes(keyAddress, keyLength);
            if (org.Length == 0 || key.Length == 0)
            {
                dtp.PushUl(0);
                dtp.PushUl(0);
                return;
            }

            int keyWidth = CharacterWidthAt(key, 0);
            ushort keyCharacter = (ushort)(keyWidth == 2 ? key[0] << 8 | key[1] : key[0]);
            uint characterPosition = 1;
            for (int position = 0; org.Length - position >= key.Length; characterPosition++)
            {
                int width = CharacterWidthAt(org, position);
                ushort character = (ushort)(width == 2
                    ? org[position] << 8 | org[position + 1]
                    : org[position]);
                if (character == keyCharacter && org.AsSpan(position, key.Length).SequenceEqual(key))
                {
                    uint consumed = checked((uint)(position + key.Length));
                    searchRemainAddress = AddAddress(orgAddress, consumed);
                    searchRemainLength = orgLength - consumed;
                    dtp.PushUl(characterPosition);
                    dtp.PushUl(0);
                    return;
                }
                position += width;
            }

            dtp.PushUl(0);
            dtp.PushUl(0);
        }

        private void SearchByteString()
        {
            uint keyLength = dtp.PopUl();
            uint keyAddress = dtp.PopUl();
            uint orgLength = dtp.PopUl();
            uint orgAddress = dtp.PopUl();
            byte[] org = ReadStringBytes(orgAddress, orgLength);
            byte[] key = ReadStringBytes(keyAddress, keyLength);
            uint bytePosition = 0;
            if (org.Length > 0 && key.Length > 0 && key.Length <= org.Length)
            {
                for (int position = 0; position <= org.Length - key.Length; position++)
                {
                    if (org.AsSpan(position, key.Length).SequenceEqual(key))
                    {
                        bytePosition = checked((uint)position + 1);
                        break;
                    }
                }
            }
            dtp.PushUl(bytePosition);
            dtp.PushUl(0);
        }

        private void CompareStringBytes() => PushComparison(ReadStringPair(), CompareByteStrings);

        private void CompareStringCharacters(bool ignoreAsciiCase) =>
            PushComparison(ReadStringPair(), (left, right) => CompareCharacterStrings(left, right, ignoreAsciiCase));

        private (byte[] left, byte[] right) ReadStringPair()
        {
            uint rightLength = dtp.PopUl();
            uint rightAddress = dtp.PopUl();
            uint leftLength = dtp.PopUl();
            uint leftAddress = dtp.PopUl();
            return (ReadStringBytes(leftAddress, leftLength), ReadStringBytes(rightAddress, rightLength));
        }

        private void PushComparison((byte[] left, byte[] right) strings, Func<byte[], byte[], uint> compare)
        {
            dtp.PushUl(compare(strings.left, strings.right));
            dtp.PushUl(0);
        }

        private static uint CompareByteStrings(byte[] left, byte[] right)
        {
            int commonLength = Math.Min(left.Length, right.Length);
            int comparison = left.AsSpan(0, commonLength).SequenceCompareTo(right.AsSpan(0, commonLength));
            if (comparison == 0)
                comparison = left.Length.CompareTo(right.Length);
            return comparison < 0 ? 1u : comparison > 0 ? 2u : 0u;
        }

        private static uint CompareCharacterStrings(byte[] left, byte[] right, bool ignoreAsciiCase)
        {
            int leftPosition = 0;
            int rightPosition = 0;
            while (leftPosition < left.Length && rightPosition < right.Length)
            {
                int leftWidth = CharacterWidthAt(left, leftPosition);
                int rightWidth = CharacterWidthAt(right, rightPosition);
                ushort leftCharacter = (ushort)(leftWidth == 2
                    ? left[leftPosition] << 8 | left[leftPosition + 1]
                    : left[leftPosition]);
                ushort rightCharacter = (ushort)(rightWidth == 2
                    ? right[rightPosition] << 8 | right[rightPosition + 1]
                    : right[rightPosition]);
                if (ignoreAsciiCase)
                {
                    leftCharacter = ToUpperAscii(leftCharacter);
                    rightCharacter = ToUpperAscii(rightCharacter);
                }

                if (leftCharacter != rightCharacter)
                    return leftCharacter < rightCharacter ? 1u : 2u;
                leftPosition += leftWidth;
                rightPosition += rightWidth;
            }

            return leftPosition == left.Length
                ? (rightPosition == right.Length ? 0u : 1u)
                : 2u;
        }

        private static ushort ToUpperAscii(ushort value) =>
            value is >= (ushort)'a' and <= (ushort)'z' ? (ushort)(value - ('a' - 'A')) : value;

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