using System.Text.Json;
using System.Text.Json.Serialization;
using DB.Conversion.Feed;
using Domain.Feed;
using Domain.Processing;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Feed;

internal sealed class FeedRowConfiguration : IModelConfiguration
{
	private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

	public void Apply(ModelBuilder modelBuilder) =>
		modelBuilder.Entity<FeedRow>(builder =>
		{
			builder.HasUserOwner();
			builder.HasIndex(UserOwnedConfigurationExtensions.UserIdColumn);
			builder
				.Property(x => x.Filter)
				.HasConversion(v => JsonSerializer.Serialize(v, JsonOptions), v => JsonSerializer.Deserialize<QueryFilter>(v, JsonOptions)!);
			builder.Property(x => x.Sort).HasConversion(v => JsonSerializer.Serialize(v, JsonOptions), v => JsonSerializer.Deserialize<QuerySort>(v, JsonOptions)!);
			builder.Property(x => x.Limit).HasConversion(v => JsonSerializer.Serialize(v, JsonOptions), v => JsonSerializer.Deserialize<QueryLimit>(v, JsonOptions)!);
			builder.Property(x => x.SyncMode).HasConversion(new SyncModeConverter());
			builder.Property(x => x.Strategy).HasConversion(new FeedStrategyConverter());
		});
}
