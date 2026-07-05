using System.Diagnostics;
using System.Reflection;

namespace Observability.Tracing;

public class TracingProxy<T> : DispatchProxy
	where T : class
{
	private T _target = null!;

	public static T Wrap(T target)
	{
		var proxy = Create<T, TracingProxy<T>>();
		((TracingProxy<T>)(object)proxy!)._target = target;
		return proxy;
	}

	protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
	{
		var name = $"{typeof(T).Name}.{targetMethod!.Name}";
		var returnType = targetMethod.ReturnType;

		if (returnType.IsGenericType)
		{
			var definition = returnType.GetGenericTypeDefinition();
			var itemType = returnType.GetGenericArguments()[0];
			if (definition == typeof(Task<>))
				return Dispatch(nameof(InvokeTaskResult), itemType, targetMethod, args, name);
			if (definition == typeof(ValueTask<>))
				return Dispatch(nameof(InvokeValueTaskResult), itemType, targetMethod, args, name);
			if (definition == typeof(IAsyncEnumerable<>))
				return Dispatch(nameof(InvokeStream), itemType, targetMethod, args, name);
		}

		if (returnType == typeof(Task))
			return InvokeTask(targetMethod, args, name);
		if (returnType == typeof(ValueTask))
			return InvokeValueTask(targetMethod, args, name);

		using var activity = Instrumentation.Source.StartActivity(name);
		try
		{
			return targetMethod.Invoke(_target, args);
		}
		catch (TargetInvocationException exception) when (exception.InnerException is not null)
		{
			Fail(activity, exception.InnerException);
			throw exception.InnerException;
		}
	}

	private object Dispatch(string helper, Type itemType, MethodInfo targetMethod, object?[]? args, string name) =>
		typeof(TracingProxy<T>)
			.GetMethod(helper, BindingFlags.NonPublic | BindingFlags.Instance)!
			.MakeGenericMethod(itemType)
			.Invoke(this, [targetMethod, args, name])!;

	private async Task InvokeTask(MethodInfo targetMethod, object?[]? args, string name)
	{
		using var activity = Instrumentation.Source.StartActivity(name);
		try
		{
			await (Task)targetMethod.Invoke(_target, args)!;
		}
		catch (Exception exception)
		{
			Fail(activity, exception);
			throw;
		}
	}

	private async Task<TResult> InvokeTaskResult<TResult>(MethodInfo targetMethod, object?[]? args, string name)
	{
		using var activity = Instrumentation.Source.StartActivity(name);
		try
		{
			return await (Task<TResult>)targetMethod.Invoke(_target, args)!;
		}
		catch (Exception exception)
		{
			Fail(activity, exception);
			throw;
		}
	}

	private async ValueTask InvokeValueTask(MethodInfo targetMethod, object?[]? args, string name)
	{
		using var activity = Instrumentation.Source.StartActivity(name);
		try
		{
			await (ValueTask)targetMethod.Invoke(_target, args)!;
		}
		catch (Exception exception)
		{
			Fail(activity, exception);
			throw;
		}
	}

	private async ValueTask<TResult> InvokeValueTaskResult<TResult>(MethodInfo targetMethod, object?[]? args, string name)
	{
		using var activity = Instrumentation.Source.StartActivity(name);
		try
		{
			return await (ValueTask<TResult>)targetMethod.Invoke(_target, args)!;
		}
		catch (Exception exception)
		{
			Fail(activity, exception);
			throw;
		}
	}

	private async IAsyncEnumerable<TItem> InvokeStream<TItem>(MethodInfo targetMethod, object?[]? args, string name)
	{
		using var activity = Instrumentation.Source.StartActivity(name);
		var source = (IAsyncEnumerable<TItem>)targetMethod.Invoke(_target, args)!;
		var count = 0;
		await foreach (var item in source)
		{
			count++;
			yield return item;
		}
		activity?.SetTag("sammlarr.yielded", count);
	}

	private static void Fail(Activity? activity, Exception exception)
	{
		if (activity is null)
		{
			return;
		}
		activity.AddException(exception);
		activity.SetStatus(ActivityStatusCode.Error, exception.Message);
	}
}
