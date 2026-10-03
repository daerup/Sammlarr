using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Media;

namespace DB.Data.Ratings;

[Table("RatingDefaults")]
internal record RatingDefaultRow : Entity<Guid>, IUserOwned
{
	[Required]
	public required RatingStrategy Strategy { get; set; }
}
