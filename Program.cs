using Microsoft.IdentityModel.Tokens;
using System.Text;
using ApiMarketCatalystBlack.GeneralDefinitions;
using ApiMarketCatalystBlack.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JWTSettings"));
builder.Services.AddScoped<PlatformService>();

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JWTSettings").Bind(jwtSettings);

if (jwtSettings.ValidIssuer == null || jwtSettings.ValidAudience == null || jwtSettings.Secret == null)
{
	Console.WriteLine("JWT Settings not properly set.");
	Environment.Exit(1);
}

builder.Services.AddAuthentication("Bearer")
	   .AddJwtBearer(options =>
					 {
						 options.TokenValidationParameters = new TokenValidationParameters
															 {
																 // Configure your token validation parameters
																 ValidateIssuer = true,
																 ValidateAudience = true,
																 ValidateLifetime = true,
																 ValidateIssuerSigningKey = true,
																 ValidIssuer = jwtSettings.ValidIssuer,
																 ValidAudience = jwtSettings.ValidAudience,
																 IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
															 };
					 });

builder.Services.AddAuthorization();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(static options =>
						 {
							 // Development CORS Policy
							 options.AddPolicy("DevCorsPolicy", static policy =>
																{
																	policy.WithOrigins("http://localhost:3000") // Allow specific origin for development
																		  .AllowAnyHeader()
																		  .AllowAnyMethod();
																});

							 // Production CORS Policy
							 options.AddPolicy("ProdCorsPolicy", static policy =>
																 {
																	 policy.WithOrigins("https://market.catalyst.black") // Use your production domain here
																		   .AllowAnyHeader()
																		   .AllowAnyMethod();
																 });
						 });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
	app.UseDeveloperExceptionPage();
	app.UseCors("DevCorsPolicy"); // Use Development CORS Policy
}
else
	app.UseCors("ProdCorsPolicy"); // Use Production CORS Policy

// Use authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
