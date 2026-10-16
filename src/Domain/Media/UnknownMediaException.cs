namespace Domain.Media;

public sealed class UnknownMediaException(MediaId mediaId) : Exception($"Media '{mediaId.Value}' is not known.");
