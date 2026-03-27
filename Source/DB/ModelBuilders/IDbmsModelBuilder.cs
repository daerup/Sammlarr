using Microsoft.EntityFrameworkCore;

namespace DB.ModelBuilders;

internal interface IDbmsModelBuilder
{
    static abstract string Fullname { get; }
    static abstract void OnModelCreating(ModelBuilder modelBuilder);
}