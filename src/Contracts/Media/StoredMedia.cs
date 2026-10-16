using Domain.Media;

namespace Contracts.Media;

public sealed record StoredMedia(MediaIdentity Identity, Optional<CachedDetails> Cached);
