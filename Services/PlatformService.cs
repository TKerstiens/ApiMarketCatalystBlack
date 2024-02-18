using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

public class PlatformService
{
    private readonly ILogger<PlatformService> _logger;
    private readonly string _connectionString;
    private readonly string _salt;

    public PlatformService(ILogger<PlatformService> logger)
    {
        _logger = logger;

        string? salt = Environment.GetEnvironmentVariable("APPLICATION_SALT");
        string? host = Environment.GetEnvironmentVariable("DB_HOST");
        string? port = Environment.GetEnvironmentVariable("DB_PORT");
        string? dbName = Environment.GetEnvironmentVariable("DB_NAME");
        string? user = Environment.GetEnvironmentVariable("DB_USER");
        string? password = Environment.GetEnvironmentVariable("DB_PASSWORD");

        bool exiting = false;

        if(salt == null) {
            Console.Error.WriteLine("APPLICATION_SALT environment variable is not set. Application will terminate.");
            exiting = true;
        }

        if(host == null) {
            Console.Error.WriteLine("DB_HOST environment variable is not set. Application will terminate.");
            exiting = true;
        }

        if(port == null) {
            Console.Error.WriteLine("DB_PORT environment variable is not set. Application will terminate.");
            exiting = true;
        }

        if(dbName == null) {
            Console.Error.WriteLine("DB_NAME environment variable is not set. Application will terminate.");
            exiting = true;
        }

        if(user == null) {
            Console.Error.WriteLine("DB_USER environment variable is not set. Application will terminate.");
            exiting = true;
        }

        if(password == null) {
            Console.Error.WriteLine("DB_PASSWORD environment variable is not set. Application will terminate.");
            exiting = true;
        }

        if(exiting)
        {
            throw new InvalidOperationException("Environment not properly configured.");
        }

        _salt = salt!; // Null Check Above
        _connectionString = $"server={host};port={port};database={dbName};user={user};password={password}";
    }

    public byte[] HashPassword(string password)
    {
        password = _salt + password;
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return hashBytes;
        }
    }

    public async Task<UserResult> AddUser(User newUser)
    {
        // Check for missing username or password
        if (string.IsNullOrEmpty(newUser.Username) || string.IsNullOrEmpty(newUser.Password))
        {
            return new UserResult(null, "Username or password are missing.");
        }

        try
        {
            using (MySqlConnection connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Check if user already exists
                using (MySqlCommand checkCmd = new MySqlCommand("SELECT COUNT(*) FROM `Users` WHERE `Username` = @Username", connection))
                {
                    checkCmd.Parameters.AddWithValue("@Username", newUser.Username);
                    object? result = await checkCmd.ExecuteScalarAsync();
                    if(result == null) return new UserResult(null, "Unknown error, NTZ9U2H5");
                    int exists = Convert.ToInt32(result);
                    if (exists > 0)
                    {
                        return new UserResult(null, "User already exists.");
                    }
                }

                byte[] hashedPassword = HashPassword(newUser.Password);

                using (MySqlCommand command = new MySqlCommand("INSERT INTO `Users` (`Username`, `Password`) VALUES (@Username, @Password);", connection))
                {
                    command.Parameters.AddWithValue("@Username", newUser.Username);
                    command.Parameters.AddWithValue("@Password", hashedPassword);

                    await command.ExecuteNonQueryAsync();
                    newUser.ID = Convert.ToInt32(command.LastInsertedId);
                }
            }

            return new UserResult(newUser, "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while adding the user.");
            return new UserResult(null, "An error occurred while adding the user. Exception logged on server.");
        }
    }

    public async Task<string> CreateAndStoreToken(User user)
    {
        // Check for null environment variables
        string? jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
        string? jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
        string? jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");

        if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
        {
            _logger.LogError("One or more JWT configuration environment variables are not set. Application will not proceed.");
            throw new InvalidOperationException("JWT configuration environment variables are missing. JWT_KEY, JWT_ISSUER, JWT_AUDIENCE");
        }

        if(user.ID == null)
        {
            throw new InvalidOperationException("No User ID provided.");
        }

        // Generate JWT
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[] 
            {
                new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()!) // Null check above
            }),
            Expires = DateTime.UtcNow.AddDays(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = jwtIssuer,
            Audience = jwtAudience
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwtString = tokenHandler.WriteToken(token);

        // Insert JWT into database
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new MySqlCommand("INSERT INTO `Tokens` (`UserID`, `Token`, `CreatedTime`, `ExpiresTime`, `IsCanceled`) VALUES (@UserID, @Token, @CreatedTime, @ExpiresTime, @IsCanceled)", connection))
                {
                    command.Parameters.AddWithValue("@UserID", user.ID);
                    command.Parameters.AddWithValue("@Token", jwtString);
                    command.Parameters.AddWithValue("@CreatedTime", DateTime.UtcNow);
                    command.Parameters.AddWithValue("@ExpiresTime", DateTime.UtcNow.AddDays(1));
                    command.Parameters.AddWithValue("@IsCanceled", false);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while inserting the JWT into the database.");
            throw; // Propagate the exception to handle it according to your application's error handling policy
        }

        return jwtString;
    }

    public async Task<UserResult> AuthUser(User user)
    {
        if (string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.Password))
        {
            return new UserResult(null, "No Username or Password.");
        }

        try
        {
            using (MySqlConnection connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Hash the provided password
                byte[] hashedPassword = HashPassword(user.Password);

                // Prepare the query to search for the username with the hashed password and retrieve the user's ID
                string query = "SELECT `ID` FROM `Users` WHERE `Username` = @Username AND `Password` = @Password";

                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", user.Username);
                    command.Parameters.AddWithValue("@Password", hashedPassword);

                    // Execute the query and try to retrieve the user's ID
                    object? result = await command.ExecuteScalarAsync();
                    if(result == null) return new UserResult(null, "Unknown error, N43SS834");

                    if (result != null)
                    {
                        // Attach the retrieved ID to the user object
                        user.ID = Convert.ToInt32(result);
                        // Assuming CreateAndStoreToken generates a JWT for the user and stores it in the database
                        user.Token = await CreateAndStoreToken(user);
                        return new UserResult(user);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while authenticating the user.");
        }

        // Return an error if an exception occurs or if no user is found
        return new UserResult(null, "Unable to authenticate user credentials.");
    }
}

public class UserResult
{
    public User? User { get; private set; }
    public string ErrorMessage { get; private set; }
    public bool Success { get; private set; }

    public UserResult(User? user, string errorMessage = "")
    {
        User = user;
        ErrorMessage = errorMessage;
        Success = string.IsNullOrEmpty(ErrorMessage);
    }
}