FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/JellyfinReporter/JellyfinReporter.csproj", "src/JellyfinReporter/"]
RUN dotnet restore "src/JellyfinReporter/JellyfinReporter.csproj"

COPY . .
RUN dotnet build "src/JellyfinReporter/JellyfinReporter.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/JellyfinReporter/JellyfinReporter.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JellyfinReporter.dll"]
