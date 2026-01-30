using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Dao.AI.BreakPoint.ApiService.Configuration;

/// <summary>
/// Configures rate limiting policies for the API.
/// </summary>
public static class RateLimitingConfiguration
{
    public const string AnonymousPolicy = "anonymous";
    public const string AuthenticatedPolicy = "authenticated";
    public const string FixedPolicy = "fixed";

    /// <summary>
    /// Adds rate limiting services with policies for anonymous and authenticated users.
    /// Anonymous users get stricter limits to protect against abuse.
    /// </summary>
    public static IServiceCollection AddBreakPointRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global limiter based on client IP - applies to all requests
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;

                // Use user ID for authenticated users, IP address for anonymous
                var partitionKey = isAuthenticated
                    ? context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown-user"
                    : GetClientIpAddress(context);

                // Authenticated users get more generous limits
                if (isAuthenticated)
                {
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey,
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 200,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 4,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 10,
                        }
                    );
                }

                // Anonymous users get stricter limits
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2,
                    }
                );
            });

            // Named policy for stricter anonymous endpoint limiting (e.g., login, register)
            options.AddPolicy(
                AnonymousPolicy,
                context =>
                {
                    var clientIp = GetClientIpAddress(context);
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        clientIp,
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 2,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0,
                        }
                    );
                }
            );

            // Named policy for authenticated endpoints with higher limits
            options.AddPolicy(
                AuthenticatedPolicy,
                context =>
                {
                    var userId =
                        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        userId,
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 4,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 5,
                        }
                    );
                }
            );

            // Fixed window policy for specific high-traffic endpoints
            options.AddPolicy(
                FixedPolicy,
                context =>
                {
                    var clientIp = GetClientIpAddress(context);
                    return RateLimitPartition.GetFixedWindowLimiter(
                        clientIp,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 50,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 2,
                        }
                    );
                }
            );

            // Custom response for rate limit exceeded
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                var retryAfter = context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfterValue
                )
                    ? retryAfterValue.TotalSeconds
                    : 60;

                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter).ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        title = "Too Many Requests",
                        status = 429,
                        detail = "Rate limit exceeded. Please try again later.",
                        retryAfterSeconds = (int)retryAfter,
                    },
                    cancellationToken
                );
            };
        });

        return services;
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // First check X-Forwarded-For header (set by reverse proxies like Azure Container Apps)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first (original client)
            var ip = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        // Fall back to RemoteIpAddress
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
