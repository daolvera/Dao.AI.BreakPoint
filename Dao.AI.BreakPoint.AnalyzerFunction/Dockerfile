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

# Install OpenCV 4 native libraries and all dependencies for OpenCvSharp
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Core dependencies
    libgdiplus \
    libc6-dev \
    libgomp1 \
    # OpenCV 4 native libraries (required by OpenCvSharpExtern.so)
    libopencv-dev \
    # Tesseract OCR (required dependency)
    libtesseract-dev \
    tesseract-ocr \
    # Image format libraries
    libjpeg62-turbo \
    libpng16-16 \
    libtiff6 \
    libwebp7 \
    libopenjp2-7 \
    # Video codec libraries
    libavcodec60 \
    libavformat60 \
    libswscale7 \
    libavutil58 \
    # Additional dependencies
    libatlas-base-dev \
    liblapack3 \
    libhdf5-103-1 \
    && rm -rf /var/lib/apt/lists/*

# Set library path to help find OpenCV libraries
ENV LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH

# Copy published app
COPY --from=build /app/publish .

# Azure Functions host configuration
ENV AzureWebJobsScriptRoot=/app \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

EXPOSE 80

ENTRYPOINT ["dotnet", "Dao.AI.BreakPoint.AnalyzerFunction.dll"]
