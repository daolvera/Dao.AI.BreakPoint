# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS base
WORKDIR /home/site/wwwroot
EXPOSE 80

# Install OpenCV native dependencies in base stage
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    libx11-dev \
    libgomp1 \
    libgtk-3-0 \
    libatk-bridge2.0-0 \
    libgdk-pixbuf2.0-0 \
    libpango-1.0-0 \
    libcairo2 \
    libavcodec-dev \
    libavformat-dev \
    libswscale-dev \
    libv4l-dev \
    libxvidcore-dev \
    libx264-dev \
    libtbb12 \
    libdc1394-dev \
    && rm -rf /var/lib/apt/lists/*

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all project files first for restore
COPY ["Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj", "Dao.AI.BreakPoint.AnalyzerFunction/"]
COPY ["Dao.AI.BreakPoint.Data/Dao.AI.BreakPoint.Data.csproj", "Dao.AI.BreakPoint.Data/"]
COPY ["Dao.AI.BreakPoint.Services/Dao.AI.BreakPoint.Services.csproj", "Dao.AI.BreakPoint.Services/"]
COPY ["Dao.AI.BreakPoint.ServiceDefaults/Dao.AI.BreakPoint.ServiceDefaults.csproj", "Dao.AI.BreakPoint.ServiceDefaults/"]
RUN dotnet restore "Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj"

# Copy all source files including models
COPY . .
WORKDIR "/src/Dao.AI.BreakPoint.AnalyzerFunction"
RUN dotnet build "Dao.AI.BreakPoint.AnalyzerFunction.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Dao.AI.BreakPoint.AnalyzerFunction.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final

WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .

# Ensure the ONNX models directory has proper permissions
RUN chmod -R 755 /home/site/wwwroot/models || true

# Set environment variables for OpenCV
ENV LD_LIBRARY_PATH=/home/site/wwwroot \
    OPENCV_LOG_LEVEL=ERROR \
    AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true



