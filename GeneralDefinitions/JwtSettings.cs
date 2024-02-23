namespace ApiMarketCatalystBlack.GeneralDefinitions;

public sealed class JwtSettings
{
	public string? ValidIssuer { get; init; }
	public string? ValidAudience { get; init; }
	public string? Secret { get; init; }
}
