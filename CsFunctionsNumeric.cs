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
}
