using Domain.Providers;

namespace Contracts.Providers;

public readonly record struct LanePriority(LaneClass Class, long Order)
{
	private static readonly AsyncLocal<LanePriority?> ambient = new();

	private static long issued;

	public static LanePriority Interactive { get; } = new(LaneClass.Interactive, 0);

	public static LanePriority Current => ambient.Value ?? Interactive;

	public static LanePriority Next(LaneClass lane) => new(lane, Interlocked.Increment(ref issued));

	public IDisposable Enter()
	{
		var outer = ambient.Value;
		ambient.Value = this;
		return new Restore(outer);
	}

	private sealed class Restore(LanePriority? outer) : IDisposable
	{
		public void Dispose() => ambient.Value = outer;
	}
}
