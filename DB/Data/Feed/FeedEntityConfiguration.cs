using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts.Feed;
using Contracts.Processing;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Feed;

internal sealed class FeedEntityConfiguration : IModelConfiguration
{
	private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<FeedEntity>(builder =>
		{
			builder
				.Property(x => x.Filter)
				.HasConversion(v => JsonSerializer.Serialize(v, JsonOptions), v => JsonSerializer.Deserialize<QueryFilter>(v, JsonOptions)!);
			builder.Property(x => x.Sort).HasConversion(v => JsonSerializer.Serialize(v, JsonOptions), v => JsonSerializer.Deserialize<QuerySort>(v, JsonOptions)!);
			builder.Property(x => x.Limit).HasConversion(v => JsonSerializer.Serialize(v, JsonOptions), v => JsonSerializer.Deserialize<QueryLimit>(v, JsonOptions)!);
			builder.Property(x => x.SyncMode).HasConversion(v => v.ToString(), v => SyncMode.From(v));
		});
}
