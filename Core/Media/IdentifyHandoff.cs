using Contracts.Media;
using Domain.Media;

namespace Core.Media;

internal sealed record IdentifyHandoff(MediaIdentity Identity, Optional<StoredMedia> Stored);
