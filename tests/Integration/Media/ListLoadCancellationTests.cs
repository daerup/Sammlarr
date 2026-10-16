using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ListLoadCancellationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly MediaSpec Heat = new("Heat", MediaType.Movie, [Tmdb("949")]);

	private static readonly MediaSpec Inception = new("Inception", MediaType.Movie, [Tmdb("27205")]);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task LoadListAsync_WhenCancelledMidBurst_SendsNoMoreRequestsAndKeepsTheAnswersReceived()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.TmdbDescribes(Heat);
		await host.TmdbDescribes(Inception);
		var held = host.HttpStub.Hold("movie/27205");
		using var leaving = new CancellationTokenSource();
		var load = host.Resolve<IMediaService>()
			.LoadListAsync(new[] { Request(Heat), Request(Inception) }.ToAsyncEnumerable().AsListTitles(), FieldNeeds.Everything, leaving.Token)
			.InListOrderAsync();
		await using var titles = load.GetAsyncEnumerator();
		await titles.MoveNextAsync();
		await held.Arrived;

		await leaving.CancelAsync();
		var sent = host.HttpStub.Requests("");
		await DrainAsync(titles).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		await Task.Delay(TimeSpan.FromMilliseconds(300));

		await Assert.That(host.HttpStub.Requests("")).IsEqualTo(sent);
		var stored = await host.Resolve<IMediaRepository>().GetByExternalsAsync([Tmdb("949").ToExternal(MediaType.Movie)]);
		await Assert.That(stored.Single().Cached).IsSome();
	}

	private static async Task DrainAsync(IAsyncEnumerator<MediaMatchResult> titles)
	{
		while (await titles.MoveNextAsync()) { }
	}

	private static IdentifyRequest Request(MediaSpec spec) => new([.. spec.Ids.Select(id => id.ToExternal(spec.Type))], new None());
}
