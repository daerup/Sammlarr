// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.ComponentModel.DataAnnotations;
using Contracts.Media.List;

namespace DB.Data.Entities;

public record ImportList : Entity<Guid>
{
    [Required]
    public bool Enabled { get; set; }

    [Required]
    public MediaListProvider Provider { get; set; }

    [Required]
    public MediaListAddStrategy Strategy { get; set; }

    [Required]
    public string Identifier { get; set; } = string.Empty;

    public ListFilter? Filter { get; set; }
    public ListSort? Sort { get; set; }
    public int? Take { get; set; }
}
