using System.Text;

namespace Mind8Kernel
{
    public partial class Dispatcher
    {

        /*--------------------- 表示系  --------------------------------*/
        private void ZzHyouji()                	/* ;WORD ＄＄表示 */
        {
                /* Mコード=0x00C1 */
            // ULONG	count;
            // UCHAR  *addr;
            // count = POP_C;
            // addr  = POP_A;
            // #define  POP_U                ((ULONG)(*DstackPointer++))
            // #define  POP_A                ((UCHAR *)(*DstackPointer++))
            // #define  POP_C                POP_U
            // printStrinf( addr, count );
            uint count=dtp.PopUl();
            uint index=dtp.PopUl();
            byte[] chars=ReadStringBytes(index,count);
            string text = Encoding.GetEncoding("shift_jis").GetString(chars);
            Console.Write(text);

        }

        private void ZzKaigyou() => Console.Write("\n");

        private void ZzDoubleQuateHyouji()
        {
            Console.Write('"');
            ZzHyouji();
            Console.Write('"');
        }

        private void ZzIchimojiHyouji()
        {
            ushort value = unchecked((ushort)PopIntegerOfQuad());
            byte[] bytes = value > byte.MaxValue
                ? [(byte)(value >> 8), (byte)value]
                : [(byte)value];
            Console.Write(Encoding.GetEncoding("shift_jis").GetString(bytes));
        }

        private void ZzNibbleHyouji() =>
            Console.Write((PopIntegerOfQuad() & 0x0F).ToString("X1", System.Globalization.CultureInfo.InvariantCulture));

        private void ZzByteHyouji() =>
            Console.Write((PopIntegerOfQuad() & 0xFF).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));

        private void ZzWordHyouji() =>
            Console.Write((PopIntegerOfQuad() & 0xFFFF).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));

        private void ZzDoubleHyouji() =>
            Console.Write(PopIntegerOfQuad().ToString("X8", System.Globalization.CultureInfo.InvariantCulture));

        private void zzSyosuHyouji()
        {
            uint precision = PopIntegerOfQuad();
            Console.Write(PopFloat().ToString($"F{precision}", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void zzSisuHyouji()
        {
            uint precision = PopIntegerOfQuad();
            Console.Write(PopFloat().ToString($"e{precision}", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void zzQuadHyouji()
        {
            uint fraction = dtp.PopUl();
            uint integer = dtp.PopUl();
            Console.Write(integer.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
            Console.Write('|');
            Console.Write(fraction.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void zzDump() => DumpMemory(wordFormat: false);

        private void zzDumpWord() => DumpMemory(wordFormat: true);

        private void DumpMemory(bool wordFormat)
        {
            uint count = dtp.PopUl();
            uint address = dtp.PopUl();
            if (wordFormat) count = unchecked((count + 1) & 0xFFFFFFFE);
            do
            {
                uint size = Math.Min(count, 16);
                byte[] bytes = ReadStringBytes(address, size);
                Console.Write($"{address:X8} :");
                if (wordFormat)
                {
                    for (int i = 0; i < bytes.Length; i += 2)
                    {
                        ushort value = bytes[i];
                        if (i + 1 < bytes.Length) value |= (ushort)(bytes[i + 1] << 8);
                        Console.Write($" {value:X4}");
                    }
                }
                else
                {
                    foreach (byte value in bytes) Console.Write($" {value:X2}");
                }
                Console.WriteLine();
                count -= size;
                if (count != 0) address = AddAddress(address, size);
            } while (count != 0);
        }

        private void zzReturnStackMonitor()
        {
            uint current = ReturnStackAddressBit | checked(rtp.GetCurrentStackPointer() * 4);
            uint baseAddress = ReturnStackAddressBit | rtp.StackSize;
            Console.WriteLine($"ReturnStackPointer={current:X8} (base={baseAddress:X8})");
        }

        private void zzStackDump()
        {
            const string separator = "-------------------------------------------------------";
            uint stackPointer = DataStackAddressBit | checked(dtp.GetCurrentStackPointer() * 4);
            uint baseAddress = DataStackAddressBit | dtp.StackSize;

            Console.WriteLine(separator);
            Console.WriteLine($"sp={stackPointer:X8} base={baseAddress:X8}");
            if (stackPointer > baseAddress)
            {
                Console.WriteLine("スタックがアンダーフローしています．");
                Console.WriteLine(separator);
                dtp.ResetStackPointer();
                return;
            }

            uint usedBytes = baseAddress - stackPointer;
            if ((usedBytes & 7) != 0)
            {
                Console.WriteLine("スタックポインタが異常です．");
                Console.WriteLine(separator);
                dtp.ResetStackPointer();
                return;
            }

            Console.Write("スタック内容： ");
            for (uint offset = 0; offset < usedBytes; offset += 8)
            {
                uint address = AddAddress(stackPointer, offset);
                uint low = ReadMemoryWord(address);
                uint high = ReadMemoryWord(AddAddress(address, 4));
                Console.Write($"{high:X8}|{low:X8}  ");
            }
            Console.WriteLine();
            Console.WriteLine(separator);
            dtp.ResetStackPointer();
        }

        private void ZzBaiseidoHyouji()
        {
            ulong high = dtp.PopUl();
            ulong low = dtp.PopUl();
            long value = unchecked((long)((high << 32) | low));
            Console.Write(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}