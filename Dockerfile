# Use the Microsoft .NET SDK image to build the project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy the project files and build our release
COPY . ./
RUN dotnet publish -c Release -o out

# Generate runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

ENV APPLICATION_SALT "47f7f7467c7d9210b422e3ddc4bf09c3"
ENV DB_HOST "mysql-container"
ENV DB_PORT "3306"
ENV DB_NAME "MarketPlatform"
ENV DB_USER "production"
ENV DB_PASSWORD "Z`2B*r3[Nc18"

ENTRYPOINT ["dotnet", "ApiMarketCatalystBlack.dll"]