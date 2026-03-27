using System;

namespace UI.Data.Entities;

public interface IAuditable
{
	DateTime CreatedAt { get; set; }
	DateTime UpdatedAt { get; set; }
}
