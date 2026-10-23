namespace Domain.Auth;

public interface ICredential
{
	Guid ClientId { get; }

	Secret AccessToken { get; }
}
