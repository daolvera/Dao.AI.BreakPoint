# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files for restore (paths relative to AnalyzerFunction directory)
COPY ["Dao.AI.BreakPoint.AnalyzerFunction.csproj", "Dao.AI.BreakPoint.AnalyzerFunction/"]
COPY ["../Dao.AI.BreakPoint.Data/Dao.AI.BreakPoint.Data.csproj", "Dao.AI.BreakPoint.Data/"]
COPY ["../Dao.AI.BreakPoint.Services/Dao.AI.BreakPoint.Services.csproj", "Dao.AI.BreakPoint.Services/"]
COPY ["../Dao.AI.BreakPoint.ServiceDefaults/Dao.AI.BreakPoint.ServiceDefaults.csproj", "Dao.AI.BreakPoint.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj"

# Copy all source code
COPY [".", "Dao.AI.BreakPoint.AnalyzerFunction/"]
COPY ["../Dao.AI.BreakPoint.Data/", "Dao.AI.BreakPoint.Data/"]
COPY ["../Dao.AI.BreakPoint.Services/", "Dao.AI.BreakPoint.Services/"]
COPY ["../Dao.AI.BreakPoint.ServiceDefaults/", "Dao.AI.BreakPoint.ServiceDefaults/"]
COPY ["../Dao.AI.BreakPoint.Models/", "Dao.AI.BreakPoint.Models/"]

# Build and publish
RUN dotnet publish "Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# Runtime stage - Azure Functions base image
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS runtime

# Install OpenCV dependencies
# These are required for OpenCvSharp to load native libraries
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Core OpenCV dependencies
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender1 \
    libfontconfig1 \
    # Video/image processing dependencies  
    libavcodec60 \
    libavformat60 \
    libswscale7 \
    libv4l-0 \
    libatlas-base-dev \
    libgtk-3-0 \
    # Additional dependencies for video capture
    libdc1394-25 \
    libgstreamer1.0-0 \
    libgstreamer-plugins-base1.0-0 \
    # Cleanup
    && rm -rf /var/lib/apt/lists/*

# Set environment variables for Azure Functions
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true
# Ensure OpenCV can find its native libraries
ENV LD_LIBRARY_PATH=/home/site/wwwroot/runtimes/linux-x64/native:/usr/lib/x86_64-linux-gnu

WORKDIR /home/site/wwwroot
COPY --from=build /app/publish .

# Expose port for health checks
EXPOSE 80
