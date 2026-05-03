using BlazorBlueprint.Primitives.Filtering;
using Contracts.Media.List;
using Contracts.Processing;

namespace UI.Features.Import.Pages.Subscription;

public enum ScheduleMode
{
	Manual,
	Scheduled,
}

public record PreviewStatus
{
	public bool IsLoading { get; init; }
	public bool HasResults { get; init; }
	public bool HasFailed { get; init; }
	public bool HasEnrollmentChanges { get; init; }
}

public record SubscriptionConfig
{
	public bool IsEnabled { get; init; } = true;
	public string Name { get; init; } = string.Empty;
	public MediaListProvider? Provider { get; init; } = MediaListProvider.Mdblist;
	public MediaListAddStrategy Strategy { get; init; } = MediaListAddStrategy.Append;
	public string ListIdentifier { get; init; } = string.Empty;
	public FilterDefinition? Filter { get; init; }
	public QuerySort? Sort { get; init; }
	public QueryLimit? Limit { get; init; }
	public SyncMode Schedule { get; init; } = new SyncMode(new Manual());

	public bool IsTestable => Provider is not null && !string.IsNullOrWhiteSpace(ListIdentifier) && (Limit is null || Limit.Count > 0);

	public bool IsValid => IsTestable && !string.IsNullOrWhiteSpace(Name);

	public int GetPreviewHash() =>
		HashCode.Combine(HashCode.Combine(Provider, ListIdentifier, Filter is not null, Filter?.Version), HashCode.Combine(Sort, Limit));

	public int GetSaveHash() =>
		HashCode.Combine(
			HashCode.Combine(IsEnabled, Name, Provider, Strategy, ListIdentifier),
			HashCode.Combine(Filter is not null, Filter?.Version, Sort, Limit),
			Schedule
		);
}
