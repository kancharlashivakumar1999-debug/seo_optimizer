# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["SeoInsight.Web/SeoInsight.Web.csproj", "SeoInsight.Web/"]
COPY ["SeoInsight.Tests/SeoInsight.Tests.csproj", "SeoInsight.Tests/"]
RUN dotnet restore "SeoInsight.Web/SeoInsight.Web.csproj"

# Copy all source files
COPY . .

# Run automated tests during image build
RUN dotnet test "SeoInsight.Tests/SeoInsight.Tests.csproj" -c Release

# Publish release artifacts
WORKDIR "/src/SeoInsight.Web"
RUN dotnet publish "SeoInsight.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Render passes $PORT dynamically (default 10000 or 8080)
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080

EXPOSE 8080

COPY --from=build /app/publish .

# Create logs directory
RUN mkdir -p /app/Logs

ENTRYPOINT ["dotnet", "SeoInsight.Web.dll"]
