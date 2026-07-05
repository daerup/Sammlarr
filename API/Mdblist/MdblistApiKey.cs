using Contracts;
using Domain;
using Domain.Auth;
using Domain.Providers;

namespace API.Mdblist;

internal sealed class MdblistApiKey : IApiKey
{
	private MdblistApiKey(string value) => Value = value;

	public ApiKeyProvider Provider => ApiKeyProvider.Mdblist;

	[Sensitive]
	public string Value { get; }

	public static async Task<Result<IApiKey>> CreateAsync(string value, MdblistClient client, CancellationToken cancellationToken = default)
	{
		if (!await client.ValidateApiKeyAsync(value, cancellationToken))
		{
			return Error.From("The MDBList API key is not valid.");
		}

		return Success.Of<IApiKey>(new MdblistApiKey(value));
	}
}
