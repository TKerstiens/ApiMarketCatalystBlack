namespace ApiMarketCatalystBlack.Models;

public sealed class User
{
	internal int? Id { get; set; }
	internal string? Token { get; set; }
	internal string? Username { get; init; }
	internal string? Password { get; init; }
	public string? ConfirmP { get; set; }
}
