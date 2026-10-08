using Domain.Providers;

namespace Domain.Media;

public sealed class LookupNotRunException(MediaProvider target)
	: Exception($"No lookup to a {target} id could run, so the title is looked up again next time.");
