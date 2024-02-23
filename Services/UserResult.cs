using ApiMarketCatalystBlack.Models;

namespace ApiMarketCatalystBlack.Services;

internal sealed class UserResult
{
	public User? User { get; private set; }
	public string ErrorMessage { get; set; }
	public bool Success { get; private set; }

	public UserResult(User? user, string errorMessage = "")
	{
		User = user;
		ErrorMessage = errorMessage;
		Success = string.IsNullOrEmpty(ErrorMessage);
	}
}
