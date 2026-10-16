using Domain.Wantlist.Arr;

namespace Domain.Wantlist.Sync;

public readonly union WantlistSyncResult(WantlistSyncSummary, WantlistSyncAlreadyRunning, WantlistSyncSinkUnreachable);

public readonly union WantlistPlanResult(WantlistSyncSummary, WantlistSyncSinkUnreachable);

public readonly record struct WantlistSyncAlreadyRunning
{
	public override string ToString() => "Sync already in progress.";
}

public readonly record struct WantlistSyncSinkUnreachable(string Reason, ValueSet<ArrApp> Apps)
{
	public override string ToString() => $"Couldn't reach the provider - {Reason}";
}
