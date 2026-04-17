using System.Runtime.CompilerServices;

namespace Shared;

public static class EnumerableExtensions
{
	extension<TIn>(IEnumerable<TIn> source)
		where TIn : IUnion
	{
		public IEnumerable<TUnion> OfUnionType<TUnion>()
		{
			return source.Where(x => x.Value is TUnion).Select(x => (TUnion)x.Value);
		}
	}
}
