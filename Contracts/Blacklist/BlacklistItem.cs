using Domain.Media;

namespace Contracts.Blacklist;

public record BlacklistItem(IdentifiedMedia Media, DateTimeOffset AddedAt);
