using System.ComponentModel;
using System.Globalization;

namespace ContractCompatibility.Tests;

internal sealed class TestDataConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value is string valueString ? new TestData(valueString) : base.ConvertFrom(context, culture, value);
    }
}