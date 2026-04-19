using Contracts.Media.Search;
using Microsoft.AspNetCore.Components;

namespace UI.Shared.Media;

public sealed record TileIconContext(IMediaDetailResult Result, bool IsToggled, bool IsToggling, EventCallback<IMediaDetailResult> OnToggle);
