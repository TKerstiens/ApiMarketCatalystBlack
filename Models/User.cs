public class User
{
    public int? ID { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ConfirmP { get; set; }
}

public class UserDTO
{
    public int? ID { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }

    // In your service or controller, map your domain model to the DTO
    public static UserDTO FromUser(User user)
    {
        return new UserDTO
        {
            ID = user.ID,
            Username = user.Username,
            Token = user.Token
        };
    }
}