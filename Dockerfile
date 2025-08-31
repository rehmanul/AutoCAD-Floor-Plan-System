FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app
EXPOSE 10000
COPY src/Api/MinimalApi.cs ./Program.cs
COPY web ./web
RUN dotnet new web --force
ENTRYPOINT ["dotnet", "run"]