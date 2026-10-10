using Domain.Providers;
using TUnit.Assertions.Core;

namespace TestInfrastructure;

public static class AnswerAssertionExtensions
{
	extension<T>(IAssertionSource<Answer<T>> source)
		where T : notnull
	{
		public UnionAssertion<Answer<T>, T> IsFound() => source.IsOfUnionType<Answer<T>, T>();
	}
}
