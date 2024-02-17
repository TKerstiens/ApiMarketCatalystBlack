using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

[Route("[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private static List<User> users = new List<User>
    {
        new User { ID = 1, Username = "Jimmy", Password = "Nuetron" },
    };

    // GET: api/users
    [HttpGet]
    public ActionResult<IEnumerable<User>> GetProducts()
    {
        return users;
    }

    // POST: api/users
    [HttpPost]
    public ActionResult<User> CreateUser(User user)
    {
        users.Add(user);
        return CreatedAtAction(nameof(CreateUser), new { id = user.ID }, user);
    }
}