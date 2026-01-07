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

# Build the Angular app
RUN npm run build

# Use nginx to serve the app
FROM nginx:alpine

# Copy the built app to nginx (Angular 17+ outputs to browser/ subfolder)
COPY --from=build /app/dist/dao.ai.break-point.web/browser /usr/share/nginx/html

# Copy nginx configuration as template
COPY nginx.conf /etc/nginx/nginx.conf.template

# Default environment variables
ENV PORT=80
ENV API_URL=http://localhost:5000

# Expose port 80
EXPOSE 80

# Use envsubst to substitute environment variables at startup
CMD ["/bin/sh", "-c", "export API_URL=${services__breakpointapi__http__0:-$API_URL} && envsubst '${PORT} ${API_URL}' < /etc/nginx/nginx.conf.template > /etc/nginx/nginx.conf && nginx -g 'daemon off;'"]
