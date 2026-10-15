using Contracts.Identity;
using Domain.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DB.Data.Identity;

internal sealed class UserRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, IServiceScopeFactory scopes) : IUserRepository
{
	public async Task<User> GetAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return Members.ToUser(await Members.Of(ctx).AsNoTracking().SingleAsync(member => member.Row.Id == id.Value, cancellationToken));
	}

	public async Task<Optional<User>> FindByLoginAsync(AuthProvider provider, string externalUserId, CancellationToken cancellationToken = default)
	{
		var loginProvider = provider.ToString();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var found = await (
			from login in ctx.UserLogins
			join member in Members.Of(ctx) on login.UserId equals member.Row.Id
			where login.LoginProvider == loginProvider && login.ProviderKey == externalUserId
			select member
		)
			.AsNoTracking()
			.SingleOrDefaultAsync(cancellationToken);

		return Optional.Of(found).Map(Members.ToUser);
	}

	public async Task CreateAsync(User user, AuthProvider provider, string externalUserId, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = new UserRow
		{
			Id = user.Id.Value,
			UserName = $"{provider}-{externalUserId}".ToLowerInvariant(),
			DisplayName = user.DisplayName,
			TagName = user.TagName,
			AvatarUrl = user.AvatarUrl,
			JoinedAt = user.JoinedAt,
		};
		(await users.CreateAsync(row)).Ensure();
		(await users.AddToRoleAsync(row, user.Role.ToString())).Ensure();
		(await users.AddLoginAsync(row, new UserLoginInfo(provider.ToString(), externalUserId, provider.ToString()))).Ensure();
	}

	public async Task<IReadOnlyDictionary<UserId, StoredColour>> GetColoursAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.Users.AsNoTracking()
			.Select(user => new
			{
				user.Id,
				user.Hue,
				user.PictureUrl,
				user.PictureHue,
				user.PictureChroma,
			})
			.ToListAsync(cancellationToken);
		return rows.ToDictionary(
			row => UserId.From(row.Id),
			row => new StoredColour(Optional.Of(row.Hue), Optional.Of(row.PictureUrl).Map(url => new StoredPicture(url, Tone(row.PictureHue, row.PictureChroma))))
		);
	}

	public async Task StoreColourAsync(UserId id, StoredColour colour, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var tone = colour.Picture.Map(picture => picture.Tone);
		var hue = colour.GoldenHue.ToNullable();
		var url = colour.Picture.Map(picture => picture.Url).ToNullable();
		var pictureHue = tone.Map(found => found.Hue).ToNullable();
		var pictureChroma = tone.Map(found => found.Chroma).ToNullable();
		await ctx
			.Users.Where(user => user.Id == id.Value)
			.ExecuteUpdateAsync(
				row =>
					row.SetProperty(user => user.Hue, hue)
						.SetProperty(user => user.PictureUrl, url)
						.SetProperty(user => user.PictureHue, pictureHue)
						.SetProperty(user => user.PictureChroma, pictureChroma),
				cancellationToken
			);
	}

	public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var members = await Members.Of(ctx).AsNoTracking().OrderBy(member => member.Row.JoinedAt).ToListAsync(cancellationToken);
		return [.. members.Select(Members.ToUser)];
	}

	public async Task<int> CountAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Users.CountAsync(cancellationToken);
	}

	public async Task<IReadOnlyDictionary<UserId, PersonHoldings>> GetHoldingsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var feeds = await CountedAsync(ctx.Feeds.IgnoreQueryFilters(), cancellationToken);
		var wanted = await CountedAsync(ctx.Wantlist.IgnoreQueryFilters(), cancellationToken);
		var blocked = await CountedAsync(ctx.Blocklist.IgnoreQueryFilters(), cancellationToken);
		var credentials = await ctx
			.Credentials.IgnoreQueryFilters()
			.Select(credential => new { Owner = EF.Property<Guid>(credential, UserOwnedConfigurationExtensions.UserIdColumn), credential.Provider })
			.ToListAsync(cancellationToken);
		var keys = await ctx
			.ApiKeySettings.IgnoreQueryFilters()
			.Select(key => new { Owner = EF.Property<Guid>(key, UserOwnedConfigurationExtensions.UserIdColumn), key.Provider })
			.ToListAsync(cancellationToken);
		var owners = await ctx.Users.AsNoTracking().Select(user => user.Id).ToListAsync(cancellationToken);
		return owners.ToDictionary(
			UserId.From,
			owner => new PersonHoldings(
				feeds.GetValueOrDefault(owner),
				wanted.GetValueOrDefault(owner),
				blocked.GetValueOrDefault(owner),
				[
					.. credentials.Where(credential => credential.Owner == owner).SelectMany(credential => credential.Provider.Service.Remotes),
					.. keys.Where(key => key.Owner == owner).Select(key => key.Provider.Remote),
				]
			)
		);
	}

	public async Task RefreshAsync(UserId id, string displayName, string avatarUrl, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.Users.SingleAsync(user => user.Id == id.Value, cancellationToken);
		row.DisplayName = displayName;
		row.AvatarUrl = avatarUrl;
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task ChangeRoleAsync(UserId id, UserRole role, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		var row = await users.RequireAsync(id);
		(await users.RemoveFromRolesAsync(row, await users.GetRolesAsync(row))).Ensure();
		(await users.AddToRoleAsync(row, role.ToString())).Ensure();
		(await users.UpdateSecurityStampAsync(row)).Ensure();
	}

	public async Task DeleteAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var scope = scopes.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<UserManager<UserRow>>();
		(await users.DeleteAsync(await users.RequireAsync(id))).Ensure();
	}

	public async Task<Optional<UserRole>> FindRoleAsync(UserId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var role = await Members
			.Of(ctx)
			.AsNoTracking()
			.Where(member => member.Row.Id == id.Value)
			.Select(member => member.Role)
			.SingleOrDefaultAsync(cancellationToken);
		return Optional.Of(role).Map(name => Enum.Parse<UserRole>(name));
	}

	private static async Task<Dictionary<Guid, int>> CountedAsync<T>(IQueryable<T> rows, CancellationToken cancellationToken)
		where T : class
	{
		return await rows.GroupBy(row => EF.Property<Guid>(row, UserOwnedConfigurationExtensions.UserIdColumn))
			.Select(owned => new { Owner = owned.Key, Count = owned.Count() })
			.ToDictionaryAsync(owned => owned.Owner, owned => owned.Count, cancellationToken);
	}

	private static Optional<PictureTone> Tone(double? hue, double? chroma)
	{
		return Optional.Of(hue).Map(found => Optional.Of(chroma).Map(saturation => new PictureTone(found, saturation)));
	}
}
