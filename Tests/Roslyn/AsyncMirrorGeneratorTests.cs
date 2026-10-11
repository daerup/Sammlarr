using Roslyn.AsyncMirror;

namespace Roslyn.Tests;

public class AsyncMirrorGeneratorTests
{
	private const string Sample = """
		using System;
		using System.Threading.Tasks;
		using Shared;

		namespace Shared
		{
			[AttributeUsage(AttributeTargets.Method)]
			internal sealed class GenerateAsyncAttribute : Attribute;
		}

		namespace Sample
		{
			public sealed record Box<T>(T Item)
				where T : notnull;

			public sealed record Counter(int Value);

			public static partial class BoxExtensions
			{
				extension<T>(Box<T> box)
					where T : notnull
				{
					public T Value() => box.Item;

					[GenerateAsync]
					public Box<TOut> Map<TOut>(Func<T, TOut> map)
						where TOut : notnull => new(map(box.Item));

					[GenerateAsync]
					public Box<T> Visit(Action<T> visit)
					{
						visit(box.Item);
						return box;
					}
				}

				extension(Counter counter)
				{
					[GenerateAsync]
					public int Add(Func<int> step) => counter.Value + step();
				}

				extension(string text)
				{
					internal int Width() => text.Length;
				}
			}
		}
		""";

	private const string PrioritySample = """
		using System;
		using System.Runtime.CompilerServices;
		using System.Threading.Tasks;
		using Shared;

		namespace Shared
		{
			[AttributeUsage(AttributeTargets.Method)]
			internal sealed class GenerateAsyncAttribute : Attribute;
		}

		namespace Sample
		{
			public sealed record Box<T>(T Item)
				where T : notnull;

			public static partial class BoxExtensions
			{
				extension<T>(Box<T> box)
					where T : notnull
				{
					public T Or(Func<T> fallback) => box.Item;

					[OverloadResolutionPriority(-1)]
					public Task<T> Or(Func<Task<T>> fallback) => Task.FromResult(box.Item);

					[GenerateAsync]
					[System.Runtime.CompilerServices.OverloadResolutionPriority(-1)]
					public Box<T> Visit(Action<T> visit)
					{
						visit(box.Item);
						return box;
					}
				}
			}
		}
		""";

	[Test]
	public async Task Generate_ForAnExtensionBlock_EmitsTheAsyncMethodsAndTheTaskMirrors()
	{
		await Verify(await GeneratorRun.OutputAsync(new AsyncMirrorGenerator(), Sample));
	}

	[Test]
	public async Task Generate_ForAMethodWithAnOverloadPriority_CarriesItOntoItsAsyncFormAndMirrors()
	{
		await Verify(await GeneratorRun.OutputAsync(new AsyncMirrorGenerator(), PrioritySample));
	}
}
