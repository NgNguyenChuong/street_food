FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file first to leverage Docker layer caching
COPY API/StreetFoodNarrator.API/StreetFoodNarrator.API.csproj API/StreetFoodNarrator.API/
RUN dotnet restore API/StreetFoodNarrator.API/StreetFoodNarrator.API.csproj

# Copy source and publish API
COPY . .
RUN dotnet publish API/StreetFoodNarrator.API/StreetFoodNarrator.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render injects PORT at runtime; fall back to 10000 for local container runs
ENTRYPOINT ["sh", "-c", "dotnet StreetFoodNarrator.API.dll --urls http://0.0.0.0:${PORT:-10000}"]