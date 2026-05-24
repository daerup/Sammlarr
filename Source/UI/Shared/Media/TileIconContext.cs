using Contracts.Media.Search;
using Microsoft.AspNetCore.Components;

namespace UI.Shared.Media;

public sealed record TileIconContext(IMediaDetail Result, bool IsToggled, bool IsToggling, EventCallback<IMediaDetail> OnToggle);
