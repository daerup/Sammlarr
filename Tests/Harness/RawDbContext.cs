using System.Data.Common;
using System.Linq.Expressions;
using Dapper;
using DB;
using DB.Data;
using Microsoft.EntityFrameworkCore;

namespace Harness;

internal class RawDbContext(SammlarrDbContext context)
{
	public RawDbContext<TTable> Table<TTable>()
		where TTable : IIdentifiable<Guid> => new(context);
}

internal class RawDbContext<TTable>
	where TTable : IIdentifiable<Guid>
{
	private readonly SammlarrDbContext _context;

	internal RawDbContext(SammlarrDbContext context) => _context = context;

	public async Task<TColumn> QueryByIdAsync<TColumn>(Guid id, Expression<Func<TTable, TColumn>> propertySelector)
	{
		var connection = GetConnection(propertySelector, out var columnName, out var tableName);
		var sql = $"SELECT {columnName} FROM {tableName} WHERE {nameof(IIdentifiable<>.Id)} = @id";
		return await connection.QuerySingleAsync<TColumn>(sql, new { id });
	}

	public async Task<IEnumerable<TColumn>> QueryAsync<TColumn>(string columnName)
	{
		var connection = GetConnection(out var tableName);
		var sql = $"SELECT {columnName} FROM {tableName}";
		return await connection.QueryAsync<TColumn>(sql);
	}

	public async Task<IEnumerable<TColumn>> QueryAsync<TColumn>(Expression<Func<TTable, TColumn>> propertySelector)
	{
		var connection = GetConnection(propertySelector, out var columnName, out var tableName);
		var sql = $"SELECT {columnName} FROM {tableName}";
		return await connection.QueryAsync<TColumn>(sql);
	}

	private DbConnection GetConnection<TColumn>(Expression<Func<TTable, TColumn>> propertySelector, out string columnName, out string tableName)
	{
		columnName = (propertySelector.Body as MemberExpression)?.Member.Name!;
		return GetConnection(out tableName);
	}

	private DbConnection GetConnection(out string tableName)
	{
		tableName = _context.Model.FindEntityType(typeof(TTable))!.GetTableName()!;
		return _context.Database.GetDbConnection();
	}
}
