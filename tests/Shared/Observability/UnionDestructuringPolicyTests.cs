using Observability.Logging;
using Serilog;
using Serilog.Events;
using TestInfrastructure;

namespace Shared.Tests.Observability;

public class UnionDestructuringPolicyTests
{
	private sealed record Payload(string Title, int Year);

	private sealed record OptionalEnvelope(Optional<Payload> Hint);

	private sealed record ResultEnvelope(Result<Payload> Outcome);

	[Test]
	public async Task Some_UnwrapsToInnerValue_WithoutWrapper()
	{
		var hint = (StructureValue)Capture(new OptionalEnvelope(Some.Of(new Payload("Minions", 2026)))).Properties.Single(p => p.Name == "Hint").Value;
		var title = ((ScalarValue)hint.Properties.Single(p => p.Name == "Title").Value).Value;

		await Assert.That(title).IsEqualTo("Minions");
		await Assert.That(hint.Properties.Any(p => p.Name == "Value")).IsFalse();
	}

	[Test]
	public async Task None_RendersAsScalarNone()
	{
		var hint = ((ScalarValue)Capture(new OptionalEnvelope(new None())).Properties.Single(p => p.Name == "Hint").Value).Value;

		await Assert.That(hint).IsEqualTo("None");
	}

	[Test]
	public async Task Result_Value_UnwrapsToInnerValue_WithoutWrapper()
	{
		var outcome = (StructureValue)Capture(new ResultEnvelope(Success.Of(new Payload("Minions", 2026)))).Properties.Single(p => p.Name == "Outcome").Value;
		var title = ((ScalarValue)outcome.Properties.Single(p => p.Name == "Title").Value).Value;

		await Assert.That(title).IsEqualTo("Minions");
		await Assert.That(outcome.Properties.Any(p => p.Name == "Value")).IsFalse();
	}

	[Test]
	public async Task Result_Error_UnwrapsToError()
	{
		var outcome = (StructureValue)Capture(new ResultEnvelope(Error.From("boom"))).Properties.Single(p => p.Name == "Outcome").Value;
		var reason = ((ScalarValue)outcome.Properties.Single(p => p.Name == "Reason").Value).Value;

		await Assert.That(reason).IsEqualTo("boom");
	}

	private static StructureValue Capture(object graph)
	{
		var sink = new CapturingSink();
		using var logger = new LoggerConfiguration().Destructure.With(new UnionDestructuringPolicy()).WriteTo.Sink(sink).CreateLogger();
		logger.Information("{@Envelope}", graph);
		return (StructureValue)sink.Last.Properties["Envelope"];
	}
}
