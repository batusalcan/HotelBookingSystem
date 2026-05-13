FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore — cached unless .csproj files change
COPY AiAgentService/AiAgentService.csproj AiAgentService/
COPY SharedKernel/SharedKernel.csproj SharedKernel/
RUN dotnet restore AiAgentService/AiAgentService.csproj

# Copy source and publish
COPY . .
RUN dotnet publish AiAgentService/AiAgentService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AiAgentService.dll"]
