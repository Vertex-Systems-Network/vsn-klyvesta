FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Klyvesta.Api/Klyvesta.Api.csproj", "Klyvesta.Api/"]
COPY ["src/Klyvesta.Application/Klyvesta.Application.csproj", "Klyvesta.Application/"]
COPY ["src/Klyvesta.Domain/Klyvesta.Domain.csproj", "Klyvesta.Domain/"]
COPY ["src/Klyvesta.Infrastructure/Klyvesta.Infrastructure.csproj", "Klyvesta.Infrastructure/"]
RUN dotnet restore "Klyvesta.Api/Klyvesta.Api.csproj"
COPY . .
WORKDIR "/src/Klyvesta.Api"
RUN dotnet build "Klyvesta.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Klyvesta.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Klyvesta.Api.dll"]
