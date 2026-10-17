using Bunit;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using UI.Features.Home.Components;
using UI.Tests.Media;
using ErrorEventArgs = Microsoft.AspNetCore.Components.Web.ErrorEventArgs;

namespace UI.Tests.Home;

public class PosterTrackTests : BunitContext
{
	private const string Poster = "https://cdn.example.test/poster.jpg";
	private static readonly MediaId First = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Second = MediaId.From(new Guid(2, 0, 0, new byte[8]));
	private static readonly MediaId Fresh = MediaId.From(new Guid(3, 0, 0, new byte[8]));

	[Before(Test)]
	public void Setup()
	{
		JSInterop.Mode = JSRuntimeMode.Loose;
		Services.AddSingleton(ArtworkStub.Sizing());
	}

	[Test]
	public async Task Ids_WhenANewTitleIsPrependedAfterAReload_RequestsJustThatTitle()
	{
		var requests = new List<IReadOnlyList<MediaId>>();
		var cut = Render<PosterTrack>(parameters =>
			parameters
				.Add(track => track.Ids, [First, Second])
				.Add(track => track.Media, new Dictionary<MediaId, MatchedMedia>())
				.Add(track => track.OnNeedMedia, ids => requests.Add(ids))
		);

		cut.Render(parameters => parameters.Add(track => track.Ids, [Fresh, First, Second]));

		await Assert.That(requests).Count().IsEqualTo(2);
		await Assert.That(requests[1]).IsEquivalentTo([Fresh]);
	}

	[Test]
	public async Task Dispose_WhenTheBrowserClosesWhileItStopsWatching_ReportsNoError()
	{
		var watcher = JSInterop.SetupModule("./js/poster-track.js").SetupModule(invocation => invocation.Identifier == "observe");
		watcher.SetupVoid("dispose").SetException(new TaskCanceledException());
		Render<PosterTrack>(parameters => parameters.Add(track => track.Ids, [First]).Add(track => track.Media, new Dictionary<MediaId, MatchedMedia>()));

		await Renderer.Dispatcher.InvokeAsync(DisposeComponentsAsync);

		await Assert.That(Renderer.UnhandledException.IsCompleted).IsFalse();
	}

	[Test]
	public async Task Dispose_WhileItsModuleIsStillLoading_ReportsNoError()
	{
		var import = new TaskCompletionSource<IJSObjectReference>();
		var js = IJSRuntime.Mock();
		js.InvokeAsync<IJSObjectReference>(Any<string>(), Any<object?[]?>()).Returns((_, _) => new ValueTask<IJSObjectReference>(import.Task));
		Services.AddSingleton(js.Object);
		var module = IJSObjectReference.Mock();
		module
			.InvokeAsync<IJSObjectReference>(Any<string>(), Any<object?[]?>())
			.Throws(new ObjectDisposedException("DotNetObjectReference`1[[UI.Features.Home.Components.PosterTrack]]"));
		Render<PosterTrack>(parameters => parameters.Add(track => track.Ids, [First]).Add(track => track.Media, new Dictionary<MediaId, MatchedMedia>()));

		await Renderer.Dispatcher.InvokeAsync(DisposeComponentsAsync);
		await Renderer.Dispatcher.InvokeAsync(() => import.SetResult(module.Object));

		await Assert.That(Renderer.UnhandledException.IsCompleted).IsFalse();
	}

	[Test]
	public async Task ImageError_WhenTheSizedPosterFails_FallsBackToTheOriginal()
	{
		var media = new MatchedMedia(new MediaIdentity(First, []), new MovieDetails { PosterUrl = Poster });
		var cut = Render<PosterTrack>(parameters =>
			parameters.Add(track => track.Ids, [First]).Add(track => track.Media, new Dictionary<MediaId, MatchedMedia> { [First] = media })
		);
		await Assert.That(cut.Find("img.track-poster").GetAttribute("srcset")).IsEqualTo($"{Poster}?width=110 1x, {Poster}?width=220 2x");

		await cut.Find("img.track-poster").TriggerEventAsync("onerror", new ErrorEventArgs());

		await Assert.That(cut.Find("img.track-poster").HasAttribute("srcset")).IsFalse();
	}
}
