using TUnit.Assertions.Core;

namespace TestInfrastructure;

public static class ResultAssertionExtensions
{
	extension<T>(IAssertionSource<Result<T>> source)
		where T : notnull
	{
		public UnionAssertion<Result<T>, T> IsSuccess() => source.IsOfUnionType<Result<T>, T>();

		public UnionAssertion<Result<T>, Error> IsError() => source.IsOfUnionType<Result<T>, Error>();
	}
}
