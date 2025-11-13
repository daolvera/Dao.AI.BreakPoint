using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dao.AI.BreakPoint.ApiService.Configuration;

public static class AuthenticationConfiguration
{
    public static void AddBreakPointAuthenticationAndAuthorization(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

        builder.Services.AddOptionsWithValidateOnStart<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations();

        builder
            .Services.AddIdentityApiEndpoints<AppUser>()
            .AddEntityFrameworkStores<BreakPointDbContext>()
            .AddDefaultTokenProviders();

        // Use a service provider to access the configured JwtOptions
        var serviceProvider = builder.Services.BuildServiceProvider();
        var jwtOptions = serviceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;

        builder
            .Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    IssuerSigningKey = JwtTokenService.GetSecurityKey(jwtOptions.Key),
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateIssuer = true,
                    ValidAudiences = [jwtOptions.Audience],
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
            })
            .AddGoogle(options =>
            {
                options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
                options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
            });

        builder.Services.AddAuthorization();
    }
}
