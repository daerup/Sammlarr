namespace Shared.Extensions;

public static class TypeExtensions
{
	public static string GetGenericName<T>() => typeof(T).GetGenericName();

	extension(Type? type)
	{
		public string GetGenericName()
		{
			if (type is null)
			{
				return "null";
			}
			if (!type.IsGenericType)
			{
				return type.Name;
			}

			var name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];
			var args = string.Join(", ", type.GetGenericArguments().Select(GetGenericName));
			return $"{name}<{args}>";
		}
	}
}
