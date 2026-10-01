# Build stage (SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["PropertyRentalSystem.Web.csproj", "./"]
RUN dotnet restore "PropertyRentalSystem.Web.csproj"

# Copy the rest of the code and build the release
COPY . .
RUN dotnet publish "PropertyRentalSystem.Web.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Expose the web app's port
EXPOSE 8080

ENTRYPOINT ["dotnet", "PropertyRentalSystem.Web.dll"]