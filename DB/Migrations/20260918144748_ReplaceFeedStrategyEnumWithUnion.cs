using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DB.Migrations
{
	/// <inheritdoc />
	public partial class ReplaceFeedStrategyEnumWithUnion : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
				UPDATE Feeds SET Strategy = 'mirror:3' WHERE Strategy = 'Replace';
				"""
			);
			migrationBuilder.Sql(
				"""
				UPDATE Feeds SET Strategy = 'append' WHERE Strategy = 'Append';
				"""
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(
				"""
				UPDATE Feeds SET Strategy = 'Replace' WHERE Strategy LIKE 'mirror%';
				"""
			);
			migrationBuilder.Sql(
				"""
				UPDATE Feeds SET Strategy = 'Append' WHERE Strategy = 'append';
				"""
			);
		}
	}
}
