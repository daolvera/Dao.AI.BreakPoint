FROM mcr.microsoft.com/dotnet/sdk:10.0 AS installer-env

COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
mkdir -p /home/site/wwwroot && \
dotnet publish *.csproj --output /home/site/wwwroot

# To enable ssh & remote debugging on app service change the base image to the one below
# FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0-appservice
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0

# Install native dependencies for ML/AI libraries
# OpenCV dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    libc6-dev \
    libgomp1 \
    libopencv-dev \
    # TensorFlow and ONNX Runtime dependencies
    libstdc++6 \
    libunwind8 \
    # General image processing dependencies
    libjpeg62-turbo \
    libpng16-16 \
    libtiff6 \
    libwebp7 \
    # Additional dependencies for native interop
    libatomic1 \
    && rm -rf /var/lib/apt/lists/*

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    # Ensure native libraries can be found
    LD_LIBRARY_PATH=/home/site/wwwroot/runtimes/linux-x64/native:$LD_LIBRARY_PATH

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]