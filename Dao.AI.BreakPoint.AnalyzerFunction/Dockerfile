# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY ["Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj", "Dao.AI.BreakPoint.AnalyzerFunction/"]
COPY ["Dao.AI.BreakPoint.Services/Dao.AI.BreakPoint.Services.csproj", "Dao.AI.BreakPoint.Services/"]
COPY ["Dao.AI.BreakPoint.Data/Dao.AI.BreakPoint.Data.csproj", "Dao.AI.BreakPoint.Data/"]
COPY ["Dao.AI.BreakPoint.ServiceDefaults/Dao.AI.BreakPoint.ServiceDefaults.csproj", "Dao.AI.BreakPoint.ServiceDefaults/"]

# Restore packages with linux-x64 runtime
RUN dotnet restore "Dao.AI.BreakPoint.AnalyzerFunction/Dao.AI.BreakPoint.AnalyzerFunction.csproj" -r linux-x64

# Copy all source code
COPY . .

# Build and publish with explicit runtime identifier to include native assets
WORKDIR "/src/Dao.AI.BreakPoint.AnalyzerFunction"
RUN dotnet publish -c Release -r linux-x64 --self-contained false -o /app/publish

# Runtime stage with OpenCV dependencies
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install OpenCV 4 native libraries and all dependencies for OpenCvSharp
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Core dependencies
    libgdiplus \
    libc6-dev \
    libgomp1 \
    # Tesseract OCR (required dependency for OpenCvSharp)
    libtesseract5 \
    tesseract-ocr \
    libleptonica-dev \
    # Image format libraries
    libjpeg62-turbo \
    libpng16-16 \
    libtiff6 \
    libwebp7 \
    libopenjp2-7 \
    # Video codec libraries
    libavcodec59 \
    libavformat59 \
    libswscale6 \
    libavutil57 \
    libdc1394-25 \
    # Additional math/compute libraries
    libatlas-base-dev \
    liblapack3 \
    libopenblas0 \
    # GStreamer for video capture
    libgstreamer1.0-0 \
    libgstreamer-plugins-base1.0-0 \
    && rm -rf /var/lib/apt/lists/*

# Set library paths to find native libraries
ENV LD_LIBRARY_PATH=/app:/app/runtimes/linux-x64/native:/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH

# Copy published app including native runtimes
COPY --from=build /app/publish .

# Verify native library exists and set permissions
RUN if [ -f /app/runtimes/linux-x64/native/libOpenCvSharpExtern.so ]; then \
        cp /app/runtimes/linux-x64/native/libOpenCvSharpExtern.so /app/ && \
        chmod +x /app/libOpenCvSharpExtern.so; \
    fi

# Diagnostic: List native libraries and check dependencies (remove in production)
RUN echo "=== Checking for OpenCvSharp native library ===" && \
    find /app -name "*.so" -o -name "libOpenCvSharp*" 2>/dev/null && \
    echo "=== Checking library dependencies ===" && \
    if [ -f /app/libOpenCvSharpExtern.so ]; then \
        ldd /app/libOpenCvSharpExtern.so || true; \
    elif [ -f /app/runtimes/linux-x64/native/libOpenCvSharpExtern.so ]; then \
        ldd /app/runtimes/linux-x64/native/libOpenCvSharpExtern.so || true; \
    else \
        echo "WARNING: libOpenCvSharpExtern.so not found!"; \
    fi

# Azure Functions host configuration
ENV AzureWebJobsScriptRoot=/app \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

EXPOSE 80

ENTRYPOINT ["dotnet", "Dao.AI.BreakPoint.AnalyzerFunction.dll"]
