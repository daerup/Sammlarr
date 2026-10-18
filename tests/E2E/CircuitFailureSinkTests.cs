using TUnit.Assertions.Exceptions;

namespace E2E.Tests;

public class CircuitFailureSinkTests
{
	private static readonly string[] VirtualizeDisposeCancelled =
	[
		"System.Threading.Tasks.TaskCanceledException: A task was canceled.",
		"   at Microsoft.JSInterop.JSRuntime.InvokeAsync[TValue](Int64 targetInstanceId, String identifier, JSCallType callType, Object[] args)",
		"   at Microsoft.AspNetCore.Components.Web.Virtualization.VirtualizeJsInterop.DisposeAsync()",
	];

	[Test]
	public async Task AssertNone_WhenOnlyVirtualizeDisposeWasCancelled_Passes()
	{
		var crashed = Crash(VirtualizeDisposeCancelled);

		await Assert.That(() => CircuitFailureSink.AssertNone(crashed)).ThrowsNothing();
	}

	[Test]
	public async Task AssertNone_WhenAnotherCrashFollowsTheVirtualizeOne_Fails()
	{
		string[] crashed =
		[
			.. Crash(VirtualizeDisposeCancelled),
			.. Crash(
				"System.ObjectDisposedException: Cannot access a disposed object.",
				"   at UI.Features.Home.Components.PosterTrack.OnAfterRenderAsync(Boolean firstRender)"
			),
		];

		await Assert.That(() => CircuitFailureSink.AssertNone(crashed)).Throws<AssertionException>();
	}

	private static string[] Crash(params string[] exception) =>
		[
			"30: 2026-10-18T07:27:51.3280000Z [09:27:51.328 ERR] Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost Unhandled exception in circuit 'e2e'.",
			.. exception.Select((line, index) => $"{31 + index}: 2026-10-18T07:27:51.3280000Z {line}"),
		];
}
