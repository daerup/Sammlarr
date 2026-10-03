using Domain.Media;
using Domain.Media.Metadata;

namespace Contracts.Media;

public sealed record StoredMedia(MediaIdentity Identity, Optional<CachedDetails> Cached);
