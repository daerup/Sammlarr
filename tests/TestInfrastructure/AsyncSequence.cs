namespace TestInfrastructure;

public static class AsyncSequence
{
	public static async IAsyncEnumerable<T> Of<T>(params IEnumerable<T> items)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
	}
}
