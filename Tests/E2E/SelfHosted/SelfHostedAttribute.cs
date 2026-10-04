using TUnit.Core.Interfaces;

namespace E2E.Tests.SelfHosted;

[AttributeUsage(AttributeTargets.Class)]
public sealed class SelfHostedAttribute : Attribute, ITestRegisteredEventReceiver, ITestDiscoveryEventReceiver
{
	private static readonly StackLimit Limit = new();

	private static readonly ParallelGroupAttribute Group = new(nameof(SelfHosted));

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
		public int Limit => SelfHostedApp.StackCount;
	}
}
