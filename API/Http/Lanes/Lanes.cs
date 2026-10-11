using System.Collections.Concurrent;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Concurrency;
using Shared.Options;

namespace API.Http.Lanes;

internal sealed record LaneName(RemoteService Remote, string Route, string Owner = "");

internal sealed record RemoteSettings(RemoteService Remote, IProviderHttpOptions Options)
{
	public TimeSpan MaxWait => TimeSpan.FromSeconds(Options.Http.MaxWaitSeconds);

	public TimeSpan EnrichWait => TimeSpan.FromSeconds(Options.Http.EnrichWaitSeconds);
}

internal sealed class Lanes(TimeProvider time, IEnumerable<RemoteSettings> settings, IServiceProvider services) : IDisposable
{
	internal const string Rest = "api";

	private readonly IReadOnlyDictionary<RemoteService, RemoteSettings> _settings = settings.DistinctBy(each => each.Remote).ToDictionary(each => each.Remote);
	private readonly ConcurrentDictionary<LaneName, LaneGate> _gates = new();
	private readonly ConcurrentDictionary<LaneName, object> _keyed = new();
	private readonly ConcurrentDictionary<(LaneName Gate, UserId User), byte> _usedBy = new();

	public LaneGate Gate(LaneName name, Optional<UserId> user)
	{
		user.Apply(found => _usedBy.TryAdd((name, found), 0));
		return _gates.GetOrAdd(name, found => new LaneGate(Policy(found), time, Optional.Of(services.GetKeyedService<ILaneQuota>(found.Remote)), found.Owner));
	}

	public ILane<TKey, TOut> Of<TKey, TOut>(LaneName name)
		where TKey : notnull
		where TOut : notnull
	{
		return (ILane<TKey, TOut>)_keyed.GetOrAdd(name, found => new Lane<TKey, TOut>(MaxBatch(found)));
	}

	public LaneName Name(RemoteService remote, HttpRequestMessage request)
	{
		request.Options.TryGetValue(LaneHandler.Owner, out var owner);
		return new LaneName(remote, Route(remote, request.RequestUri!), owner ?? "");
	}

	public TimeSpan Patience(RemoteService remote) => Settings(remote).Map(found => found.MaxWait).GetValueOr(TimeSpan.Zero);

	public bool Admits(RemoteService remote, HttpRequestMessage request)
	{
		return Gate(Name(remote, request), new None()).Admits(LanePriority.Current, Patience(remote));
	}

	public TimeSpan Backoff(LaneName name)
	{
		var policy = Policy(name);
		return policy.Limits.Select(limit => limit.Per).DefaultIfEmpty(policy.Pause).Min();
	}

	public string Route(RemoteService remote, Uri uri)
	{
		var target = uri.GetLeftPart(UriPartial.Path);
		var options = _settings[remote].Options;
		return options
			.Http.Lanes.SelectMany(lane => lane.Value.Routes.Select(route => Prefix(options, route).Map(prefix => (Lane: lane.Key, Prefix: prefix))))
			.WhereSome()
			.Where(route => target.StartsWith(route.Prefix, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(route => route.Prefix.Length)
			.Select(route => route.Lane)
			.FirstOrNone()
			.GetValueOr(Rest);
	}

	public IEnumerable<LaneLoad> Loads(RemoteService remote, Optional<UserId> user, LaneClass asking)
	{
		return _gates
			.Where(gate => gate.Key.Remote == remote)
			.Where(gate => gate.Key.Owner == "" || user.Match(found => _usedBy.ContainsKey((gate.Key, found)), () => false))
			.Select(gate => gate.Value.Load(asking));
	}

	public Optional<RemoteSettings> Settings(RemoteService remote) => _settings.GetValueOrNone(remote);

	public void Dispose()
	{
		foreach (var gate in _gates.Values)
		{
			gate.Dispose();
		}
	}

	private static Optional<string> Prefix(IProviderHttpOptions options, LaneRouteOptions route)
	{
		return options.Urls.GetValueOrNone(route.Url).Map(url => $"{url.TrimEnd('/')}/{route.Path.Trim('/')}".TrimEnd('/'));
	}

	private int MaxBatch(LaneName name)
	{
		return _settings[name.Remote].Options.Http.Lanes.GetValueOrNone(name.Route).Map(lane => lane.MaxBatch).GetValueOr(1);
	}

	private LanePolicy Policy(LaneName name)
	{
		return _settings[name.Remote]
			.Options.Http.Lanes.GetValueOrNone(name.Route)
			.Map(lane => new LanePolicy(
				[.. lane.Limits.Select(limit => new RateWindow(limit.Requests, limit.Per, limit.Aligned))],
				lane.MaxInFlight,
				lane.FailuresToDown,
				TimeSpan.FromSeconds(lane.DownSeconds)
			))
			.GetValueOr(LanePolicy.Unlimited);
	}
}
