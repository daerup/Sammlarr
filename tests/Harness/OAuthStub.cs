using Contracts.Auth;

namespace Harness;

internal sealed class OAuthStub : IOAuthProvider
{
	private Result<ExternalLogin> _completion = Error.From("No OAuth completion configured.");

	private Func<ExternalLogin, Result<ExternalLogin>> _asAdmin = login => Success.Of(login);

	internal void Returns(ExternalLogin login) => _completion = Success.Of(login);

	internal void Fails(string reason) => _completion = Error.From(reason);

	internal void RefusesAdmin(string reason) => _asAdmin = _ => Error.From(reason);

	public Task<Result<string>> BeginLoginAsync(string callbackUrl, CancellationToken cancellationToken = default) =>
		Task.FromResult<Result<string>>(Success.Of("https://auth.plex.tv/link"));

	public Task<Result<ExternalLogin>> CompleteLoginAsync(string callbackQuery, CancellationToken cancellationToken = default) => Task.FromResult(_completion);

	public Result<ExternalLogin> AsAdmin(ExternalLogin login) => _asAdmin(login);
}
