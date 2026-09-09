using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<E2E.Tests.E2EParallelLimit>]

namespace E2E.Tests;

public sealed class E2EParallelLimit : IParallelLimit
{
	public int Limit => 4;
}
