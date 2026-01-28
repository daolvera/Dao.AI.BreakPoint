#!/bin/sh
set -e

# Get API URL from Aspire service discovery environment variables
# Aspire provides these in format: services__<name>__<scheme>__0
# Check HTTPS first (for Azure Container Apps with external endpoints), then HTTP, then fallback
API_URL="${services__breakpointapi__https__0:-${services__breakpointapi__http__0:-${API_URL:-http://localhost:5000}}}"

echo "=========================================="
echo "Container startup configuration"
echo "=========================================="
echo "API_URL: $API_URL"
echo "PORT: ${PORT:-80}"
echo ""
echo "Available service discovery environment variables:"
env | grep -i "services__" || echo "  (none found)"
echo "=========================================="

# Generate runtime environment config for Angular
# This injects the API URL at container startup, not build time
cat > /usr/share/nginx/html/env-config.js << EOF
(function(window) {
  window.__env = window.__env || {};
  window.__env.apiUrl = '$API_URL';
})(this);
EOF

echo "Generated env-config.js with API_URL: $API_URL"

# Create simplified nginx configuration (no API proxy - frontend calls API directly)
cat > /etc/nginx/nginx.conf << EOF
events {
    worker_connections 1024;
}

http {
    include       /etc/nginx/mime.types;
    default_type  application/octet-stream;

    sendfile        on;
    keepalive_timeout  65;

    # Logging
    log_format main '\$remote_addr - \$remote_user [\$time_local] "\$request" '
                    '\$status \$body_bytes_sent "\$http_referer" '
                    '"\$http_user_agent"';

    access_log /var/log/nginx/access.log main;
    error_log /var/log/nginx/error.log warn;

    server {
        listen ${PORT:-80};
        server_name _;
        root /usr/share/nginx/html;
        index index.html;

        # Health check endpoint for container orchestration
        location /health {
            access_log off;
            return 200 '{"status":"healthy"}';
            add_header Content-Type application/json;
        }

        # Handle Angular routing - serve index.html for all routes
        location / {
            try_files \$uri \$uri/ /index.html;
        }
    }
}
EOF

echo "=========================================="
echo "Generated nginx.conf:"
echo "=========================================="
cat /etc/nginx/nginx.conf
echo "=========================================="

# Test nginx configuration
echo "Testing nginx configuration..."
nginx -t

# Start nginx
echo "Starting nginx..."
exec nginx -g 'daemon off;'
