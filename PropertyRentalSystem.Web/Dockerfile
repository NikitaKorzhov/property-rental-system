# Етап збірки (SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копіюємо файл проєкта та відновлюємо залежності
COPY ["PropertyRentalSystem.Web.csproj", "./"]
RUN dotnet restore "PropertyRentalSystem.Web.csproj"

# Копіюємо весь решту коду та збираємо реліз
COPY . .
RUN dotnet publish "PropertyRentalSystem.Web.csproj" -c Release -o /app/publish

# Етап виконання (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Відкриваємо порт для вебзастосунку
EXPOSE 8080

ENTRYPOINT ["dotnet", "PropertyRentalSystem.Web.dll"]