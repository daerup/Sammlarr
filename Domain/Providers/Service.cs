using Domain.Wantlist.Arr;

namespace Domain.Providers;

public readonly union Service(RemoteService, Arr, SammlarrAccounts)
{
	public ValueSet<RemoteService> Remotes
	{
		get
		{
			return this switch
			{
				RemoteService remote => [remote],
				Arr => [.. Enum.GetValues<ArrApp>().Select(app => app.Remote)],
				SammlarrAccounts => [],
			};
		}
	}

	public bool DependsOn(RemoteService remote) => Remotes.Contains(remote);

	public static bool operator ==(Service left, Service right) => Equals(left.Value, right.Value);

	public static bool operator !=(Service left, Service right) => !Equals(left.Value, right.Value);

	public override bool Equals(object? obj) => obj is Service other && this == other;

	public override int GetHashCode() => Value?.GetHashCode() ?? 0;

	public override string ToString()
	{
		return this switch
		{
			RemoteService remote => remote.ToString(),
			Arr => nameof(Arr),
			SammlarrAccounts => nameof(SammlarrAccounts),
		};
	}
}

public readonly record struct Arr;

public readonly record struct SammlarrAccounts;
