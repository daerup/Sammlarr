namespace Shared;

public readonly record struct None;

public record Some<T>(T Value) where T : notnull
{
    public static implicit operator Some<T>(T value) => new(value);
}

public static class Some
{
    public static Some<T> Of<T>(T value) where T : notnull => new(value);
    public static async Task<Some<T>> Of<T>(Task<T> value) where T : notnull => new(await value);
}

public static class Optional
{
    public static Optional<T> Of<T>(T? value) where T : class => value is not null ? Some.Of(value) : new None();

    public static async Task<Optional<T>> Of<T>(Task<T?> value) where T : class => Of(await value);
}

public union Optional<T>(None, Some<T>) where T : notnull;
