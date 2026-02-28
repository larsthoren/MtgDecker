# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY src/MtgDecker.Domain/MtgDecker.Domain.csproj src/MtgDecker.Domain/
COPY src/MtgDecker.Application/MtgDecker.Application.csproj src/MtgDecker.Application/
COPY src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj src/MtgDecker.Infrastructure/
COPY src/MtgDecker.Engine/MtgDecker.Engine.csproj src/MtgDecker.Engine/
COPY src/MtgDecker.Web/MtgDecker.Web.csproj src/MtgDecker.Web/
RUN dotnet restore src/MtgDecker.Web/MtgDecker.Web.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/MtgDecker.Web/MtgDecker.Web.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MtgDecker.Web.dll"]
