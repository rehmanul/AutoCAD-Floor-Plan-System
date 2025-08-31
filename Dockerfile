FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 10000
COPY src/Api/MinimalApi.cs ./MinimalApi.cs
COPY web ./web
ENTRYPOINT ["dotnet", "run", "MinimalApi.cs"]