using Domain.Auth;

namespace Core.Auth;

internal static class AccessChecks
{
	internal static Result<TSecret> Answer<TSecret>(AccessCheck check, TSecret secret, string service, string secretName)
		where TSecret : notnull
	{
		return check switch
		{
			AccessCheck.Valid => Success.Of(secret),
			AccessCheck.Rejected => Error.From($"The {service} {secretName} is not valid."),
			AccessCheck.Unreachable => Error.From($"{service} could not be reached to check the {secretName}. Try again in a moment."),
			AccessCheck.Spent => Error.From($"{service} is limiting requests right now. Try the {secretName} again in a moment."),
		};
	}
}
