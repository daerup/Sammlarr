namespace Domain.Auth;

public interface ICredential
{
	Guid ClientId { get; }

	string AccessToken { get; }
}
