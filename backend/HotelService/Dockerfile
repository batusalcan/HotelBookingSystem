FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore — cached unless .csproj files change
COPY HotelService/HotelService.csproj HotelService/
COPY SharedKernel/SharedKernel.csproj SharedKernel/
RUN dotnet restore HotelService/HotelService.csproj

# Copy source and publish
COPY . .
RUN dotnet publish HotelService/HotelService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "HotelService.dll"]
