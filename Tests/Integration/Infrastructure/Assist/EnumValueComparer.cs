using Reqnroll.Assist;

namespace Integration.Tests.Infrastructure.Assist;

public sealed class EnumValueComparer : IValueComparer
{
	public bool CanCompare(object actualValue) => actualValue?.GetType().IsEnum == true;

	public bool Compare(string expectedValue, object actualValue) =>
		Enum.TryParse(actualValue.GetType(), expectedValue?.Trim(), ignoreCase: true, out var expected) && actualValue.Equals(expected);
}
