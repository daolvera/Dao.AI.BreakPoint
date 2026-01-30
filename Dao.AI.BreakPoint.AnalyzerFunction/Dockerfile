# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj", "Dao.AI.BreakPoint.AnalyzerFunction/"]
COPY ["Dao.AI.BreakPoint.Data/Dao.AI.BreakPoint.Data.csproj", "Dao.AI.BreakPoint.Data/"]
COPY ["Dao.AI.BreakPoint.Services/Dao.AI.BreakPoint.Services.csproj", "Dao.AI.BreakPoint.Services/"]
COPY ["Dao.AI.BreakPoint.ServiceDefaults/Dao.AI.BreakPoint.ServiceDefaults.csproj", "Dao.AI.BreakPoint.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj"

# Copy source code
COPY . .

# Build the project
WORKDIR "/src/Dao.AI.BreakPoint.AnalyzerFunction"
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - Azure Functions base image
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS final

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
    libavcodec59 \
    libavformat59 \
    libswscale6 \
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
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    # Ensure OpenCV can find its native libraries
    LD_LIBRARY_PATH=/home/site/wwwroot/runtimes/linux-x64/native:$LD_LIBRARY_PATH

WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .

# Expose port for health checks
EXPOSE 80
