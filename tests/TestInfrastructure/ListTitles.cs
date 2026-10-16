using Contracts.Media;

namespace TestInfrastructure;

public static class ListTitles
{
	public static IAsyncEnumerable<ListTitle> Of(params IEnumerable<IdentifyRequest> requests) => requests.ToAsyncEnumerable().AsListTitles();

	extension(IAsyncEnumerable<IdentifyRequest> requests)
	{
		public IAsyncEnumerable<ListTitle> AsListTitles() => requests.Select(ListTitle (request) => request);
	}
}
