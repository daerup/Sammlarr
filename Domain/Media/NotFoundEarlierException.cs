using Domain.Providers;

namespace Domain.Media;

public sealed class NotFoundEarlierException(MediaProvider target)
	: Exception($"No lookup found a {target} id for these ids recently, so they were not looked up again.");
