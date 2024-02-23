using ApiMarketCatalystBlack.Models;
using ApiMarketCatalystBlack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiMarketCatalystBlack.Controllers;

[Route("[controller]")]
[ApiController]
public class UserController(PlatformService platformService) : ControllerBase
{
	private static readonly List<User> Users =
	[
		new User { Id = 1, Username = "Jimmy", Password = "Neutron" }
	];

	// GET: api/users
	[HttpGet]
	public ActionResult<IEnumerable<User>> GetProducts()
		=> Users;

	// POST: /user
	[HttpPost]
	public async Task<ActionResult<UserDto>> CreateUser(User user)
	{
		if (string.IsNullOrEmpty(user.Username))
			return BadRequest(new { error = "Username not present." });

		if (string.IsNullOrEmpty(user.Password) || string.IsNullOrEmpty(user.ConfirmP))
			return BadRequest(new { error = "Password not present." });

		if (user.Password != user.ConfirmP)
			return BadRequest(new { error = "Password confirmation does not match." });

		var result = await platformService.AddUser(user);

		if (result.Success)
			return CreatedAtAction(null, null, UserDto.FromUser(user));

		return BadRequest(new { error = result.ErrorMessage });
	}

	// POST: /user/auth
	[Route("auth")]
	[HttpPost]
	public async Task<ActionResult<UserDto>> AuthUser(User user)
	{
		if (string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.Password))
			return Unauthorized(new { error = "Unable to authenticate credentials." });

		var result = await platformService.AuthUser(user);
		return result is { Success: true, User: not null }
				   ? Ok(UserDto.FromUser(result.User))
				   : Unauthorized(new { error = "Unable to authenticate credentials." });
	}

	// GET: /user/auth/check/Admin
	[Route("auth/check/Admin")]
	[Authorize(Roles = "Admin")]
	[HttpGet]
	public ActionResult<AuthCheckResponse> CheckAdmin()
		=> new AuthCheckResponse();

	// GET: /user/auth/check/DataConsumer
	[Route("auth/check/DataConsumer")]
	[Authorize(Roles = "DataConsumer")]
	[HttpGet]
	public ActionResult<AuthCheckResponse> CheckDataConsumer()
		=> new AuthCheckResponse();
}
