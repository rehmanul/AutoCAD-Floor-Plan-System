FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Api/FloorPlanAPI.csproj", "src/Api/"]
RUN dotnet restore "src/Api/FloorPlanAPI.csproj"
COPY . .
WORKDIR "/src/src/Api"
RUN dotnet build "FloorPlanAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FloorPlanAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY src/Api/wwwroot ./wwwroot
ENTRYPOINT ["dotnet", "FloorPlanAPI.dll"]