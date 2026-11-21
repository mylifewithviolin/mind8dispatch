using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Mind8Kernel;

public partial class Dispatcher
{
    private static readonly Regex FloatPrefix = new(
        @"^[\t\n\v\f\r ]*[+-]?(?:Inf(?:inity)?|NaN|(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private void C_atof()
    {
        uint endPointerAddress = PopIntegerOfQuad();
        uint stringLength = dtp.PopUl();
        uint stringAddress = dtp.PopUl();
        string text = Encoding.ASCII.GetString(ReadStringBytes(stringAddress, stringLength));
        Match match = FloatPrefix.Match(text);
        double value = 0;
        if (match.Success)
        {
            string numericPrefix = match.Value.TrimStart();
            if (numericPrefix.EndsWith("inf", StringComparison.OrdinalIgnoreCase))
                value = numericPrefix.StartsWith("-", StringComparison.Ordinal)
                    ? double.NegativeInfinity
                    : double.PositiveInfinity;
            else if (!double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException($"c_atof could not parse numeric prefix '{match.Value}'.");
        }
        WriteMemoryWord(endPointerAddress, AddAddress(stringAddress, checked((uint)match.Length)));
        PushFloatConstant(value);
    }

    private void C_conv_to_string_quad()
    {
        uint capacity = dtp.PopUl();
        uint address = dtp.PopUl();
        uint low = dtp.PopUl();
        uint high = dtp.PopUl();
        long value = unchecked((long)(((ulong)high << 32) | low));
        WriteStringVariable(address, capacity, value.ToString(CultureInfo.InvariantCulture), truncate: false);
    }

    private void C_conv_to_string_fraction()
    {
        uint exponential = PopIntegerOfQuad();
        uint capacity = dtp.PopUl();
        uint address = dtp.PopUl();
        uint fractionDigits = PopIntegerOfQuad();
        uint fieldWidth = PopIntegerOfQuad();
        double value = PopFloat();

        int precision = checked((int)fractionDigits);
        string format = exponential == 0 ? $"F{precision}" : $"e{precision}";
        string text = value.ToString(format, CultureInfo.InvariantCulture);
        if (fieldWidth > text.Length)
            text = text.PadLeft(checked((int)fieldWidth));
        WriteStringVariable(address, capacity, text, truncate: true);
    }

    private void WriteStringVariable(uint address, uint capacity, string text, bool truncate)
    {
        if (capacity == 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "String buffer capacity must include room for a null terminator.");

        uint bufferAddress = AddAddress(address, 8);
        AddAddress(bufferAddress, capacity);
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        uint textCapacity = capacity - 1;
        if ((uint)bytes.Length > textCapacity && !truncate)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Formatted value does not fit in the string buffer.");

        int length = checked((int)Math.Min((uint)bytes.Length, textCapacity));
        WriteMemory(address, new byte[8]);
        WriteMemory(bufferAddress, bytes.AsSpan(0, length));
        WriteMemory(AddAddress(bufferAddress, checked((uint)length)), [0]);
        WriteMemoryWord(address, checked((uint)length));
        dtp.PushUl(bufferAddress);
        dtp.PushUl(checked((uint)length));
    }

    private void PushFloatConstant(double value)
    {
        ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        dtp.PushUl((uint)(bits >> 32));
        dtp.PushUl((uint)bits);
    }

    private void CMZero() => PushFloatConstant(0.0);
    private void CMOne() => PushFloatConstant(1.0);
    private void CME() => PushFloatConstant(Math.E);
    private void CMLog2E() => PushFloatConstant(Math.Log2(Math.E));
    private void CMLog10E() => PushFloatConstant(Math.Log10(Math.E));
    private void CMLn2() => PushFloatConstant(Math.Log(2.0));
    private void CMLn10() => PushFloatConstant(Math.Log(10.0));
    private void CMPi() => PushFloatConstant(Math.PI);
    private void CMPi2() => PushFloatConstant(Math.PI / 2.0);
    private void CMPi4() => PushFloatConstant(Math.PI / 4.0);
    private void CM1Pi() => PushFloatConstant(1.0 / Math.PI);
    private void CM2Pi() => PushFloatConstant(2.0 / Math.PI);
    private void CM2SqrtPi() => PushFloatConstant(2.0 / Math.Sqrt(Math.PI));
    private void CMSqrt2() => PushFloatConstant(Math.Sqrt(2.0));
    private void CM_SQRT1_2() => PushFloatConstant(1.0 / Math.Sqrt(2.0));

    private void StackPointer()
    {
        dtp.PushUl(DataStackAddressBit | checked(dtp.GetCurrentStackPointer() * 4));
        dtp.PushUl(DataStackAddressBit | dtp.StackSize);
    }

    private void ReturnStackPointer()
    {
        dtp.PushUl(ReturnStackAddressBit | checked(rtp.GetCurrentStackPointer() * 4));
        dtp.PushUl(ReturnStackAddressBit | rtp.StackSize);
    }

    private void zzDataArear()
    {
        dtp.PushUl(DataAddressBit);
        dtp.PushUl(ap.DataSize);
    }

    private void seisubuWoEru() => dtp.SetUlOffset(0, 0);

    private void syosubuWoEru()
    {
        dtp.SetUlOffset(dtp.GetUlOffset(0), 1);
        dtp.SetUlOffset(0, 0);
    }

    private void seisubuWoClear() => dtp.SetUlOffset(0, 1);

    private void seisuToSyosuWoPack()
    {
        uint fraction = dtp.PopUl();
        uint integer = dtp.PopUl();
        dtp.PushUl(integer);
        dtp.PushUl(fraction);
    }

    private void seisuToSyosuWoUnpack()
    {
        uint fraction = dtp.PopUl();
        uint integer = dtp.PopUl();
        dtp.PushUl(integer);
        dtp.PushUl(0);
        dtp.PushUl(fraction);
        dtp.PushUl(0);
    }

    private void WordStore11()
    {
        uint address = PopIntegerOfQuad();
        ushort value = unchecked((ushort)PopIntegerOfQuad());
        if (value < 256) value += 0x1100;
        WriteMemory(address, [(byte)value, (byte)(value >> 8)]);
    }

    private int PopSignedIntegerOfQuad() => unchecked((int)PopIntegerOfQuad());

    private void ApplySignedIntegerComparison(Func<int, int, bool> comparison)
    {
        int right = PopSignedIntegerOfQuad();
        int left = PopSignedIntegerOfQuad();
        dtp.PushUl(comparison(left, right) ? 1u : 0u);
        dtp.PushUl(0);
    }

    private void Kake()
    {
        int right = PopSignedIntegerOfQuad();
        int left = unchecked((int)dtp.GetUlOffset(1));
        dtp.SetUlOffset(unchecked((uint)(left * right)), 1);
    }

    private void SyouToAmari()
    {
        int divisor = PopSignedIntegerOfQuad();
        int dividend = PopSignedIntegerOfQuad();
        int quotient = dividend / divisor;
        int remainder = dividend % divisor;
        dtp.PushUl(unchecked((uint)quotient));
        dtp.PushUl(0);
        dtp.PushUl(unchecked((uint)remainder));
        dtp.PushUl(0);
    }

    private void Amari()
    {
        int divisor = PopSignedIntegerOfQuad();
        int dividend = unchecked((int)dtp.GetUlOffset(1));
        dtp.SetUlOffset(unchecked((uint)(dividend % divisor)), 1);
    }

    private void Ika() => ApplySignedIntegerComparison((left, right) => left <= right);
    private void Kotonaru() => ApplySignedIntegerComparison((left, right) => left != right);

    private void TisaiHou()
    {
        int right = PopSignedIntegerOfQuad();
        int left = PopSignedIntegerOfQuad();
        dtp.PushUl(unchecked((uint)Math.Min(left, right)));
        dtp.PushUl(0);
    }

    private void OokiiHou()
    {
        int right = PopSignedIntegerOfQuad();
        int left = PopSignedIntegerOfQuad();
        dtp.PushUl(unchecked((uint)Math.Max(left, right)));
        dtp.PushUl(0);
    }

    private void IsHanni()
    {
        int maximum = PopSignedIntegerOfQuad();
        int minimum = PopSignedIntegerOfQuad();
        int value = PopSignedIntegerOfQuad();
        dtp.PushUl(value >= minimum && value <= maximum ? 1u : 0u);
        dtp.PushUl(0);
    }

    private void DotirakaniHitosii()
    {
        int second = PopSignedIntegerOfQuad();
        int first = PopSignedIntegerOfQuad();
        int value = PopSignedIntegerOfQuad();
        dtp.PushUl(value == first || value == second ? 1u : 0u);
        dtp.PushUl(0);
    }

    private void Zettaiti()
    {
        int value = PopSignedIntegerOfQuad();
        dtp.PushUl(unchecked((uint)(value < 0 ? -value : value)));
        dtp.PushUl(0);
    }

    private void PairDeFukusya()
    {
        uint integer1 = dtp.GetUlOffset(3);
        uint fraction1 = dtp.GetUlOffset(2);
        uint integer2 = dtp.GetUlOffset(1);
        uint fraction2 = dtp.GetUlOffset(0);
        dtp.PushUl(integer1);
        dtp.PushUl(fraction1);
        dtp.PushUl(integer2);
        dtp.PushUl(fraction2);
    }

    private void IsSei() => dtp.SetUlOffset(unchecked((int)dtp.GetUlOffset(1)) >= 0 ? 1u : 0u, 1);
    private void IsSeiNoKazu() => dtp.SetUlOffset(unchecked((int)dtp.GetUlOffset(1)) > 0 ? 1u : 0u, 1);

    private void JyogeNibbleKoukan()
    {
        uint value = dtp.GetUlOffset(1);
        uint lowByte = value & 0xFF;
        dtp.SetUlOffset((value & 0xFFFFFF00) | ((lowByte >> 4 | lowByte << 4) & 0xFF), 1);
    }

    private void JyogeByteKoukan()
    {
        uint value = dtp.GetUlOffset(1);
        uint lowWord = value & 0xFFFF;
        dtp.SetUlOffset((value & 0xFFFF0000) | (lowWord >> 8 | lowWord << 8) & 0xFFFF, 1);
    }

    private void JyogeWordKoukan()
    {
        uint value = dtp.GetUlOffset(1);
        dtp.SetUlOffset((value << 16) | (value >> 16), 1);
    }

    private void DoubleSwap()
    {
        uint value = dtp.GetUlOffset(1);
        uint lowWord = value & 0xFFFF;
        uint highWord = value >> 16;
        uint swappedLow = (lowWord >> 8) | (lowWord << 8);
        uint swappedHigh = (highWord >> 8) | (highWord << 8);
        dtp.SetUlOffset(((swappedLow & 0xFFFF) << 16) | (swappedHigh & 0xFFFF), 1);
    }

    private void WatasihaBigEndian()
    {
        dtp.PushUl(BitConverter.IsLittleEndian ? 0u : 1u);
        dtp.PushUl(0);
    }

    private void WatasihaLittleEndian()
    {
        dtp.PushUl(BitConverter.IsLittleEndian ? 1u : 0u);
        dtp.PushUl(0);
    }

    private void JyouiNibble() => dtp.SetUlOffset((dtp.GetUlOffset(1) & 0xFF) >> 4, 1);
    private void KaiNibble() => dtp.SetUlOffset(dtp.GetUlOffset(1) & 0x0F, 1);
    private void JyouiByte() => dtp.SetUlOffset((dtp.GetUlOffset(1) & 0xFFFF) >> 8, 1);
    private void KaiByte() => dtp.SetUlOffset(dtp.GetUlOffset(1) & 0xFF, 1);
    private void JyouiWord() => dtp.SetUlOffset(dtp.GetUlOffset(1) >> 16, 1);
    private void KaiWord() => dtp.SetUlOffset(dtp.GetUlOffset(1) & 0xFFFF, 1);

    private void HidariShift()
    {
        uint count = unchecked((ushort)PopIntegerOfQuad());
        dtp.SetUlOffset(dtp.GetUlOffset(1) << (int)count, 1);
    }

    private void MigiShift()
    {
        uint count = unchecked((ushort)PopIntegerOfQuad());
        dtp.SetUlOffset(dtp.GetUlOffset(1) >> (int)count, 1);
    }

    private void ApplyIntegerBinary(Func<uint, uint, uint> operation)
    {
        uint right = PopIntegerOfQuad();
        dtp.SetUlOffset(operation(dtp.GetUlOffset(1), right), 1);
    }

    private void ZzAnd() => ApplyIntegerBinary((left, right) => left & right);
    private void ZzOr() => ApplyIntegerBinary((left, right) => left | right);
    private void ZzXor() => ApplyIntegerBinary((left, right) => left ^ right);
    private void ZzNot() => dtp.SetUlOffset(dtp.GetUlOffset(1) ^ uint.MaxValue, 1);
    private void ZzAndDimm() => dtp.SetUlOffset(dtp.GetUlOffset(1) & mp.FetchMcodeInt(), 1);
    private void ZzOrWimm() => dtp.SetUlOffset(dtp.GetUlOffset(1) | mp.FetchMcode(), 1);
    private void ZzOrDimm() => dtp.SetUlOffset(dtp.GetUlOffset(1) | mp.FetchMcodeInt(), 1);

    private void Pack()
    {
        uint lowWord = PopIntegerOfQuad() & 0xFFFF;
        uint highWord = dtp.GetUlOffset(1) & 0xFFFF;
        dtp.SetUlOffset((highWord << 16) | lowWord, 1);
    }

    private void Unpack()
    {
        uint value = dtp.GetUlOffset(1);
        dtp.SetUlOffset(value >> 16, 1);
        dtp.PushUl(value & 0xFFFF);
        dtp.PushUl(0);
    }

    private void Mitasu()
    {
        uint length = PopIntegerOfQuad();
        uint address = PopIntegerOfQuad();
        byte value = unchecked((byte)PopIntegerOfQuad());
        byte[] bytes = new byte[checked((int)length)];
        Array.Fill(bytes, value);
        WriteMemory(address, bytes);
    }

    private void Tenso()
    {
        uint length = PopIntegerOfQuad();
        uint target = PopIntegerOfQuad();
        uint source = PopIntegerOfQuad();
        byte[] bytes = ReadStringBytes(source, length);
        WriteMemory(target, bytes);
    }

    private void Syousuumadewaru()
    {
        double divisor = PopSignedIntegerOfQuad();
        double dividend = PopSignedIntegerOfQuad();
        PushFloat(dividend / divisor);
    }

    private void Syousuka() => PushFloat(PopSignedIntegerOfQuad());

    private void Seisuka()
    {
        int value = unchecked((int)PopFloat());
        dtp.PushUl(unchecked((uint)value));
        dtp.PushUl(0);
    }

    private void ApplyFloatUnary(Func<double, double> operation) => PushFloat(operation(PopFloat()));

    private void ApplyFloatBinary(Func<double, double, double> operation)
    {
        double right = PopFloat();
        double left = PopFloat();
        PushFloat(operation(left, right));
    }

    private void ApplyFloatComparison(Func<double, double, bool> comparison)
    {
        double right = PopFloat();
        double left = PopFloat();
        dtp.PushUl(comparison(left, right) ? 1u : 0u);
        dtp.PushUl(0);
    }

    private void Fkuwae() => ApplyFloatBinary((left, right) => left + right);
    private void Fhiku() => ApplyFloatBinary((left, right) => left - right);
    private void Fkake() => ApplyFloatBinary((left, right) => left * right);
    private void Fwaru() => ApplyFloatBinary((left, right) => left / right);
    private void Fhitosii() => ApplyFloatComparison((left, right) => left == right);
    private void Fookii() => ApplyFloatComparison((left, right) => left > right);
    private void Ftiisai() => ApplyFloatComparison((left, right) => left < right);
    private void Fijyou() => ApplyFloatComparison((left, right) => left >= right);
    private void Fika() => ApplyFloatComparison((left, right) => left <= right);
    private void Fkotonaru() => ApplyFloatComparison((left, right) => left != right);

    private void C_acos() => ApplyFloatUnary(Math.Acos);
    private void C_asin() => ApplyFloatUnary(Math.Asin);
    private void C_atan() => ApplyFloatUnary(Math.Atan);
    private void C_cos() => ApplyFloatUnary(Math.Cos);
    private void C_sin() => ApplyFloatUnary(Math.Sin);
    private void C_tan() => ApplyFloatUnary(Math.Tan);
    private void C_cosh() => ApplyFloatUnary(Math.Cosh);
    private void C_sinh() => ApplyFloatUnary(Math.Sinh);
    private void C_tanh() => ApplyFloatUnary(Math.Tanh);
    private void C_exp() => ApplyFloatUnary(Math.Exp);
    private void C_log() => ApplyFloatUnary(Math.Log);
    private void C_log10() => ApplyFloatUnary(Math.Log10);
    private void C_pow() => ApplyFloatBinary(Math.Pow);
    private void C_sqrt() => ApplyFloatUnary(Math.Sqrt);
    private void C_fabs() => ApplyFloatUnary(Math.Abs);
    private void C_radian() => ApplyFloatUnary(value => (value * Math.PI) / 180.0);
    private void C_degree() => ApplyFloatUnary(value => (value * 180.0) / Math.PI);
}
