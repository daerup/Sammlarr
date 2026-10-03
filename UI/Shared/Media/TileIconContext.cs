using Domain.Media;
using Microsoft.AspNetCore.Components;

namespace UI.Shared.Media;

public sealed record TileIconContext(MatchedMedia Result, bool IsToggled, bool IsToggling, EventCallback<MatchedMedia> OnToggle);
