using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

[Route("[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly PlatformService _platformService;
    private static List<User> users = new List<User>
    {
        new User { ID = 1, Username = "Jimmy", Password = "Nuetron" },
    };

    public UserController(PlatformService platformService)
    {
        _platformService = platformService;
    }

    // GET: api/users
    [HttpGet]
    public ActionResult<IEnumerable<User>> GetProducts()
    {
        return users;
    }

    // POST: /user
    [HttpPost]
    public async Task<ActionResult<UserDTO>> CreateUser(User user)
    {
        if(string.IsNullOrEmpty(user.Username))
        {
            var errorResponse = new { error = "Username not present." };
            return BadRequest(errorResponse);
        }

        if(string.IsNullOrEmpty(user.Password) || string.IsNullOrEmpty(user.ConfirmP))
        {
            var errorResponse = new { error = "Password not present." };
            return BadRequest(errorResponse);
        }

        if(user.Password != user.ConfirmP)
        {
            var errorResponse = new { error = "Password confirmation does not match." };
            return BadRequest(errorResponse);
        }

        UserResult result = await _platformService.AddUser(user);

        if(!result.Success)
        {
            var errorResponse = new { error = result.ErrorMessage };
            return BadRequest(errorResponse);
        }

        return CreatedAtAction(null, null, UserDTO.FromUser(user));
    }

    // POST: /user/auth
    [Route("auth")]
    [HttpPost]
    public async Task<ActionResult<UserDTO>> AuthUser(User user)
    {
        if(string.IsNullOrEmpty(user.Username))
        {
            var errorResponse = new { error = "Username not present." };
            return Unauthorized(errorResponse);
        }

        if(string.IsNullOrEmpty(user.Password))
        {
            var errorResponse = new { error = "Password not present." };
            return Unauthorized(errorResponse);
        }

        UserResult result = await _platformService.AuthUser(user);

        if(!result.Success)
        {
            var errorResponse = new { error = "Unable to authenticate credentials." };
            return Unauthorized(errorResponse);
        }

        return Ok(UserDTO.FromUser(result.User!));
    }

    // GET: /user/auth/check/Admin
    [Route("auth/check/Admin")]
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public ActionResult<AuthCheckResponse> CheckAdmin()
    {
        return new AuthCheckResponse();
    }

    // GET: /user/auth/check/DataConsumer
    [Route("auth/check/DataConsumer")]
    [Authorize(Roles = "DataConsumer")]
    [HttpGet]
    public ActionResult<AuthCheckResponse> CheckDataConsumer()
    {
        return new AuthCheckResponse();
    }
}

public class AuthCheckResponse
{
    public bool IsAuthorized { get; set; } = true;
}