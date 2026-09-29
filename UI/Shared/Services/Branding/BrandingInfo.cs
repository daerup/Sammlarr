using Domain.Providers;

namespace UI.Shared.Services.Branding;

public sealed record BrandingInfo(MediaProvider Provider, string DisplayName, string NickName, string BrandColor, BrandArt Mark, BrandArt Logo, BrandTile Tile);

public sealed record BrandArt(string Light, string Dark)
{
	public BrandArt(string file)
		: this(file, file) { }

	public bool IsThemed => Light != Dark;
}

public sealed record BrandTile(string Background, BrandArt Art, int ArtPercent);
