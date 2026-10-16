namespace UI.Shared.Announcing;

internal sealed class CoalescedReload(Func<Task> reload)
{
	private bool _running;

	private bool _again;

	internal async Task RunAsync()
	{
		if (_running)
		{
			_again = true;
			return;
		}

		_running = true;
		try
		{
			do
			{
				_again = false;
				await reload();
			} while (_again);
		}
		finally
		{
			_running = false;
		}
	}
}
