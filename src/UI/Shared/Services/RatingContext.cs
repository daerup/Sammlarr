using Contracts.Ratings;
using Domain.Media;

namespace UI.Shared.Services;

internal sealed class RatingContext(IRatingService ratings)
{
	private Task<RatingView>? _view;

	public Task<RatingView> GetAsync() => _view ??= LoadAsync();

	public void Refresh() => _view = null;

	private async Task<RatingView> LoadAsync() => new(await ratings.GetDefaultAsync(), await ratings.GetAvailableAsync());
}

internal readonly record struct RatingView(RatingStrategy Default, ValueSet<RatingSource> Available);
