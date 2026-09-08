using Contracts.Auth;

namespace Harness;

internal sealed class OAuthStub : IOAuthProvider
{
	private Result<ExternalLogin> _completion = Error.From("No OAuth completion configured.");

	internal void Returns(ExternalLogin login) => _completion = Success.Of(login);

	internal void Fails(string reason) => _completion = Error.From(reason);

	public Task<string> BeginLoginAsync(string callbackUrl, CancellationToken cancellationToken = default) => Task.FromResult("https://auth.plex.tv/link");

	public Task<Result<ExternalLogin>> CompleteLoginAsync(string callbackQuery, CancellationToken cancellationToken = default) => Task.FromResult(_completion);
}
