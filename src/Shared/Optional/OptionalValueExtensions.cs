namespace Shared;

public static class OptionalValueExtensions
{
	extension<T>(Optional<T> optional)
		where T : struct
	{
		public T? ToNullable()
		{
			return optional switch
			{
				Some<T>(var value) => value,
				None => null,
			};
		}
	}
}
