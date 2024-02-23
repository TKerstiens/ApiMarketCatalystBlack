namespace ApiMarketCatalystBlack.Services;

internal abstract class UtilityService
{
	public static bool GetEnvironmentVariable(string environmentVariable, out string? value)
	{
		value = Environment.GetEnvironmentVariable(environmentVariable);
		if (value is not null)
			return true;

		Console.Error.WriteLine($"{environmentVariable} environment variable is not set. Application will terminate.");
		return false;
	}
}
