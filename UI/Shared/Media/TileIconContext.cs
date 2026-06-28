using Microsoft.AspNetCore.Components;

namespace UI.Shared.Media;

public sealed record TileIconContext(MediaLookup Result, bool IsToggled, bool IsToggling, EventCallback<MediaLookup> OnToggle);
