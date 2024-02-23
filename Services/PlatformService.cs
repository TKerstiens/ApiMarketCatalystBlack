using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ApiMarketCatalystBlack.GeneralDefinitions;
using ApiMarketCatalystBlack.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;

namespace ApiMarketCatalystBlack.Services;

public sealed class PlatformService
{
	private readonly ILogger<PlatformService> _logger;
	private readonly JwtSettings _jwtSettings;

	private readonly string _connectionString;
	private readonly string _salt = string.Empty;

	public PlatformService(ILogger<PlatformService> logger, IOptions<JwtSettings> jwtSettings)
	{
		_logger = logger;
		_jwtSettings = jwtSettings.Value;

		var isValidConfiguration = UtilityService.GetEnvironmentVariable("APPLICATION_SALT", out var salt);
		if (salt is not null) _salt = salt;

		isValidConfiguration &= UtilityService.GetEnvironmentVariable("DB_HOST", out var host);
		isValidConfiguration &= UtilityService.GetEnvironmentVariable("DB_PORT", out var port);
		isValidConfiguration &= UtilityService.GetEnvironmentVariable("DB_NAME", out var dbName);
		isValidConfiguration &= UtilityService.GetEnvironmentVariable("DB_USER", out var user);
		isValidConfiguration &= UtilityService.GetEnvironmentVariable("DB_PASSWORD", out var password);

		if (!isValidConfiguration)
			throw new InvalidOperationException("Environment not properly configured.");

		_connectionString = $"server={host};port={port};database={dbName};user={user};password={password}";
	}

	private byte[] HashPassword(string password)
	{
		password = _salt + password;
		var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
		return hashBytes;
	}

	internal async Task<UserResult> AddUser(User newUser)
	{
		// Check for missing username or password
		if (string.IsNullOrEmpty(newUser.Username) || string.IsNullOrEmpty(newUser.Password))
			return new (null, "Username or password are missing.");

		try
		{
			await using var connection = new MySqlConnection(_connectionString);
			await connection.OpenAsync();

			// Check if user already exists
			await using var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM `Users` WHERE `Username` = @Username", connection);
			checkCmd.Parameters.AddWithValue("@Username", newUser.Username);
			var result = await checkCmd.ExecuteScalarAsync();
			if (result == null) return new (null, "Unknown error, NTZ9U2H5");
			var exists = Convert.ToInt32(result);
			if (exists > 0)
				return new (null, "User already exists.");

			var hashedPassword = HashPassword(newUser.Password);

			await using MySqlCommand command = new MySqlCommand("INSERT INTO `Users` (`Username`, `Password`) VALUES (@Username, @Password);", connection);
			command.Parameters.AddWithValue("@Username", newUser.Username);
			command.Parameters.AddWithValue("@Password", hashedPassword);

			await command.ExecuteNonQueryAsync();
			newUser.Id = Convert.ToInt32(command.LastInsertedId);

			return new (newUser, "");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while adding the user.");
			return new (null, "An error occurred while adding the user. Exception logged on server.");
		}
	}

	private async Task<string> CreateAndStoreToken(User user)
	{
		// Check for null environment variables
		var jwtKey = _jwtSettings.Secret;
		var jwtIssuer = _jwtSettings.ValidIssuer;
		var jwtAudience = _jwtSettings.ValidAudience;

		if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
		{
			_logger.LogError("One or more JWT configuration variables are not set. Application will not proceed.");
			throw new InvalidOperationException("JWT configuration variables are missing. Check appsettings for JWTSettings.");
		}

		if (user.Id == null)
			throw new InvalidOperationException("No User ID provided.");

		// Generate JWT
		var tokenExpires = DateTime.UtcNow.AddDays(1);

		var claims = new List<Claim>
					 {
						 new Claim(type: ClaimTypes.NameIdentifier, value: user.Id.ToString() ?? string.Empty), // Null Check Above
						 new Claim(ClaimTypes.Role, "DataConsumer")
					 };
		if (user.Username is "tkerstiens")
			claims.Add(new Claim(ClaimTypes.Role, "Admin"));

		var tokenHandler = new JwtSecurityTokenHandler();
		var key = Encoding.ASCII.GetBytes(jwtKey);
		var tokenDescriptor = new SecurityTokenDescriptor
							  {
								  Subject = new (claims),
								  Expires = tokenExpires,
								  SigningCredentials = new (new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
								  Issuer = jwtIssuer,
								  Audience = jwtAudience
							  };

		var token = tokenHandler.CreateToken(tokenDescriptor) as JwtSecurityToken;
		var jwtString = tokenHandler.WriteToken(token);

		// Insert JWT into database
		try
		{
			await using var connection = new MySqlConnection(_connectionString);
			await connection.OpenAsync();
			await using var command =
				new
					MySqlCommand("INSERT INTO `Tokens` (`UserID`, `Token`, `CreatedTime`, `ExpiresTime`, `IsCanceled`) VALUES (@UserID, @Token, @CreatedTime, @ExpiresTime, @IsCanceled)",
								 connection);
			command.Parameters.AddWithValue("@UserID", user.Id);
			command.Parameters.AddWithValue("@Token", jwtString);
			command.Parameters.AddWithValue("@CreatedTime", tokenExpires.AddDays(-1));
			command.Parameters.AddWithValue("@ExpiresTime", tokenExpires);
			command.Parameters.AddWithValue("@IsCanceled", false);

			await command.ExecuteNonQueryAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while inserting the JWT into the database.");
			throw; // Propagate the exception to handle it according to your application's error handling policy
		}

		return jwtString;
	}

	internal async Task<UserResult> AuthUser(User user)
	{
		if (string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.Password))
			return new UserResult(null, "No Username or Password.");

		try
		{
			await using var connection = new MySqlConnection(_connectionString);
			await connection.OpenAsync();

			// Hash the provided password
			var hashedPassword = HashPassword(user.Password);

			// Prepare the query to search for the username with the hashed password and retrieve the user's ID
			const string query = "SELECT `ID` FROM `Users` WHERE `Username` = @Username AND `Password` = @Password";

			await using var command = new MySqlCommand(query, connection);
			command.Parameters.AddWithValue("@Username", user.Username);
			command.Parameters.AddWithValue("@Password", hashedPassword);

			// Execute the query and try to retrieve the user's ID
			var result = await command.ExecuteScalarAsync();
			if (result == null) return new (null, "Unknown error, N43SS834");

			// Attach the retrieved ID to the user object
			user.Id = Convert.ToInt32(result);
			// Assuming CreateAndStoreToken generates a JWT for the user and stores it in the database
			user.Token = await CreateAndStoreToken(user);
			return new UserResult(user);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while authenticating the user.");
		}

		// Return an error if an exception occurs or if no user is found
		return new (null, "Unable to authenticate user credentials.");
	}
}
