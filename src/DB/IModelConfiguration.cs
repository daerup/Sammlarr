using Microsoft.EntityFrameworkCore;

namespace DB;

internal interface IModelConfiguration
{
	void Apply(ModelBuilder modelBuilder);
}
