namespace Contracts;

public readonly record struct None;

public record Some<T>(T Value) where T : notnull
{
    public static implicit operator Some<T>(T value) => new(value);
}

public static class Some
{
    public static Some<T> Of<T>(T value) where T : notnull => new(value);
}

public union Optional<T>(None, Some<T>) where T : notnull;
