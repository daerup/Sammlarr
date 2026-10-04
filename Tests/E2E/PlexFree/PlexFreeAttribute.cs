using TUnit.Core.Interfaces;

namespace E2E.Tests.PlexFree;

[AttributeUsage(AttributeTargets.Class)]
public sealed class PlexFreeAttribute : Attribute, ITestRegisteredEventReceiver, ITestDiscoveryEventReceiver
{
	private static readonly StackLimit Limit = new();

	private static readonly ParallelGroupAttribute Group = new(nameof(PlexFree));

	public int Order => 1;

	public ValueTask OnTestRegistered(TestRegisteredContext context)
	{
		context.ClearParallelLimiter();
		context.SetParallelLimiter(Limit);
		return ValueTask.CompletedTask;
	}

	public ValueTask OnTestDiscovered(DiscoveredTestContext context) => Group.OnTestDiscovered(context);

	private sealed class StackLimit : IParallelLimit
	{
		public int Limit => PlexFreeApp.StackCount;
	}
}
