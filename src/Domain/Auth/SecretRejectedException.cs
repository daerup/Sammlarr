namespace Domain.Auth;

public sealed class SecretRejectedException(string reason) : Exception(reason);
