namespace Shared;

public static class OptionalReferenceExtensions
{
	extension<T>(Optional<T> optional)
		where T : class
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
