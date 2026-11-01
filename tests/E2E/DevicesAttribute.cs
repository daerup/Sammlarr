using TUnit.Core.Interfaces;

namespace E2E.Tests;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class DevicesAttribute(params Device[] devices) : DataSourceGeneratorAttribute<Device>, ITestDiscoveryEventReceiver
{
	public ValueTask OnTestDiscovered(DiscoveredTestContext context)
	{
		context.SetDisplayName($"{context.GetDisplayName()} on {context.TestDetails.TestClassArguments.OfType<Device>().Single()}");
		return ValueTask.CompletedTask;
	}

	protected override IEnumerable<Func<Device>> GenerateDataSources(DataGeneratorMetadata dataGeneratorMetadata)
	{
		return devices.Select(device => (Func<Device>)(() => device));
	}
}
