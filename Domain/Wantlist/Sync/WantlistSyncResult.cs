using Domain.Wantlist.Arr;

namespace Domain.Wantlist.Sync;

public union WantlistSyncResult(WantlistSyncSummary, WantlistSyncAlreadyRunning, WantlistSyncSinkUnreachable);

public union WantlistPlanResult(WantlistSyncSummary, WantlistSyncSinkUnreachable);

public readonly record struct WantlistSyncAlreadyRunning
{
	public override string ToString() => "Sync already in progress.";
}

public readonly record struct WantlistSyncSinkUnreachable(string Reason, ValueSet<ArrApp> Apps)
{
	public override string ToString() => $"Couldn't reach the provider - {Reason}";
}
