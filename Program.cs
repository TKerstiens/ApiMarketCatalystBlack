var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<PlatformService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    // Development CORS Policy
    options.AddPolicy("DevCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Allow specific origin for development
              .AllowAnyHeader()
              .AllowAnyMethod();
    });

    // Production CORS Policy
    options.AddPolicy("ProdCorsPolicy", policy =>
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
{
    app.UseHttpsRedirection();
    app.UseCors("ProdCorsPolicy"); // Use Production CORS Policy
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
