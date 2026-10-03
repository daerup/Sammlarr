using Domain.Media;
using Microsoft.AspNetCore.Components;

namespace UI.Shared.Media;

public sealed record TileIconContext(IdentifiedMedia Result, bool IsToggled, bool IsToggling, EventCallback<IdentifiedMedia> OnToggle);
