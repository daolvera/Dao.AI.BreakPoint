# Use Node.js base image
FROM node:20-alpine AS build

# Set working directory
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Build the Angular app for production
RUN npm run build

# Use nginx to serve the app
FROM nginx:alpine

# Copy the built app to nginx (Angular 17+ outputs to browser/ subfolder)
COPY --from=build /app/dist/dao.ai.break-point.web/browser /usr/share/nginx/html

# Copy the entrypoint script and fix line endings (in case of Windows CRLF)
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN sed -i 's/\r$//' /docker-entrypoint.sh && chmod +x /docker-entrypoint.sh

# Default environment variables
ENV PORT=80
ENV API_URL=http://localhost:5000

# Expose port 80
EXPOSE 80

# Use our custom entrypoint script that generates nginx config at runtime
ENTRYPOINT ["/docker-entrypoint.sh"]
