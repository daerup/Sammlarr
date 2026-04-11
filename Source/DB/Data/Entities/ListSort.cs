using System.ComponentModel;

namespace DB.Data.Entities;

public record ListSort
{
    public required string Property { get; init; }
    public required ListSortDirection Direction { get; init; }
}