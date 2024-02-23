namespace ApiMarketCatalystBlack.Models;

public sealed class UserDto
{
	public int? Id { get; set; }
	public string? Token { get; set; }
	public string? Username { get; set; }

	// In your service or controller, map your domain model to the DTO
	internal static UserDto FromUser(User user)
		=> new ()
		   {
			   Id = user.Id,
			   Username = user.Username,
			   Token = user.Token
		   };
}
