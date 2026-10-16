namespace API.Tests;

public sealed class SingleClientHttpClientFactory(HttpClient client) : IHttpClientFactory
{
	public HttpClient CreateClient(string name) => client;
}
