using Web.Instrumentation;

namespace Web.Tests.Instrumentation;

public class BlazorRouteTrackerTests
{
	private const string Conn = "conn-1";
	private const string Conn2 = "conn-2";
	private const string AbsoluteUrl = "https://localhost:5001/feeds/edit/abc";
	private const string AbsoluteUrlWithQuery = "https://localhost:5001/feeds?page=2";

	private BlazorRouteTracker _tracker = null!;

	[Before(HookType.Test)]
	public void CreateTracker() => _tracker = new BlazorRouteTracker();

	[Test]
	public async Task OnLocationChanged_AbsoluteUriWithQuery_PreservesQueryString()
	{
		var url = _tracker.UpdateAndGet(Conn, "OnLocationChanged", new object?[] { AbsoluteUrlWithQuery, null, false });

		await Assert.That(url).IsEqualTo("/feeds?page=2");
	}

	[Test]
	public async Task StartCircuit_AbsoluteUri_KeepsPathOnly()
	{
		var url = _tracker.UpdateAndGet(Conn, "StartCircuit", new object?[] { "https://localhost:5001/", AbsoluteUrl, "{}", null });

		await Assert.That(url).IsEqualTo("/feeds/edit/abc");
	}

	[Test]
	public async Task UnknownMethod_BeforeAnyNavigation_ReturnsNull()
	{
		var url = _tracker.UpdateAndGet(Conn, "UpdateRootComponents", new object?[] { "{}" });

		await Assert.That(url).IsNull();
	}

	[Test]
	public async Task UnknownMethod_AfterNavigation_ReturnsLastKnownUrl()
	{
		_tracker.UpdateAndGet(Conn, "OnLocationChanged", new object?[] { "/feeds/edit/abc", null, false });

		var url = _tracker.UpdateAndGet(Conn, "DispatchBrowserEvent", new object?[] { "{}", "{}" });

		await Assert.That(url).IsEqualTo("/feeds/edit/abc");
	}

	[Test]
	public async Task RepeatedNavigation_OverwritesPreviousUrl()
	{
		_tracker.UpdateAndGet(Conn, "OnLocationChanged", new object?[] { "/feeds", null, false });

		var url = _tracker.UpdateAndGet(Conn, "OnLocationChanged", new object?[] { "/feeds/edit/xyz", null, false });

		await Assert.That(url).IsEqualTo("/feeds/edit/xyz");
	}

	[Test]
	public async Task MultipleConnections_TrackedIndependently()
	{
		_tracker.UpdateAndGet(Conn, "OnLocationChanged", new object?[] { "/feeds", null, false });

		var otherConnectionUrl = _tracker.UpdateAndGet(Conn2, "DispatchBrowserEvent", new object?[] { "{}", "{}" });

		await Assert.That(otherConnectionUrl).IsNull();
	}

	[Test]
	public async Task Remove_ClearsStateForConnection()
	{
		_tracker.UpdateAndGet(Conn, "OnLocationChanged", new object?[] { "/feeds", null, false });
		_tracker.Remove(Conn);

		var url = _tracker.UpdateAndGet(Conn, "DispatchBrowserEvent", new object?[] { "{}", "{}" });

		await Assert.That(url).IsNull();
	}

	[Test]
	[Arguments("OnLocationChanged", "NAVIGATE")]
	[Arguments("StartCircuit", "OPEN")]
	[Arguments("UpdateRootComponents", "RENDER")]
	[Arguments("DispatchBrowserEvent", "EVENT")]
	public async Task GetFriendlyLabel_KnownHubMethod_MapsToVerb(string hubMethod, string expected)
	{
		var label = BlazorRouteTracker.GetFriendlyLabel(hubMethod);

		await Assert.That(label).IsEqualTo(expected);
	}

	[Test]
	public async Task GetFriendlyLabel_UnknownHubMethod_ReturnsNull()
	{
		var label = BlazorRouteTracker.GetFriendlyLabel("BeginInvokeDotNetFromJS");

		await Assert.That(label).IsNull();
	}
}
