using System.ComponentModel;
using System.Globalization;

namespace Shared;

[TypeConverter(typeof(SecretTypeConverter))]
public sealed record Secret
{
	private readonly string _value;

	public Secret(string value) => _value = value;

	public string Reveal() => _value;

	public override string ToString() => "***";
}

file sealed class SecretTypeConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

	public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => new Secret((string)value);
}
