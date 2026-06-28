using Microsoft.AspNetCore.Components;

namespace UI.Shared.Media;

public sealed record TileIconContext(MediaMatch Result, bool IsToggled, bool IsToggling, EventCallback<MediaMatch> OnToggle);
