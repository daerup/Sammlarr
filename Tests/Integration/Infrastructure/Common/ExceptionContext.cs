namespace Integration.Tests.Infrastructure.Common;

public sealed class ExceptionContext
{
	internal Exception? Exception { get; private set; }

	public async Task TryExecuteAsync(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			Exception = ex;
		}
	}
}
