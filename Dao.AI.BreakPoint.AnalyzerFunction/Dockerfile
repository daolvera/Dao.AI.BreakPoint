# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY ["Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj", "Dao.AI.BreakPoint.AnalyzerFunction/"]
COPY ["Dao.AI.BreakPoint.Services/Dao.AI.BreakPoint.Services.csproj", "Dao.AI.BreakPoint.Services/"]
COPY ["Dao.AI.BreakPoint.Data/Dao.AI.BreakPoint.Data.csproj", "Dao.AI.BreakPoint.Data/"]
COPY ["Dao.AI.BreakPoint.ServiceDefaults/Dao.AI.BreakPoint.ServiceDefaults.csproj", "Dao.AI.BreakPoint.ServiceDefaults/"]

# Restore packages
RUN dotnet restore "Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj"

# Copy all source code
COPY . .

# Build and publish
WORKDIR "/src/Dao.AI.BreakPoint.AnalyzerFunction"
RUN dotnet publish -c Release -o /app/publish

# Runtime stage with OpenCV dependencies
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install OpenCV native dependencies for Linux
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    libc6-dev \
    libgomp1 \
    libatlas-base-dev \
    libgtk2.0-0 \
    libdc1394-dev \
    libjpeg-dev \
    libpng-dev \
    libtiff-dev \
    libavcodec-dev \
    libavformat-dev \
    libswscale-dev \
    libv4l-dev \
    libxvidcore-dev \
    libx264-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Azure Functions host configuration
ENV AzureWebJobsScriptRoot=/app \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

EXPOSE 80

ENTRYPOINT ["dotnet", "Dao.AI.BreakPoint.AnalyzerFunction.dll"]
