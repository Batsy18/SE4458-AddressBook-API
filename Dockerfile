# Build stage: has the full SDK, compiles and publishes the app.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (only the project file), so this layer is cached when only code changes.
COPY AddressBook.Api/AddressBook.Api.csproj AddressBook.Api/
RUN dotnet restore AddressBook.Api/AddressBook.Api.csproj

COPY AddressBook.Api/ AddressBook.Api/
RUN dotnet publish AddressBook.Api/AddressBook.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage: only the ASP.NET runtime and the published output (much smaller image).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Run as the image's built-in non-root user.
USER $APP_UID

# Render sets PORT; Program.cs listens on it. 8080 is the image default otherwise.
EXPOSE 8080
ENTRYPOINT ["dotnet", "AddressBook.Api.dll"]
