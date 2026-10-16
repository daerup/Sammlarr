namespace Harness;

internal sealed class HeldRequest
{
	private readonly TaskCompletionSource _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

	internal Task Arrived => _arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));

	internal void Release() => _released.TrySetResult();

	internal async Task HoldAsync()
	{
		_arrived.TrySetResult();
		await _released.Task;
	}
}
