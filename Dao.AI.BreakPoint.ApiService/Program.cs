using Dao.AI.BreakPoint.ApiService.Endpoints;
using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowAngularApp",
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
    );
});

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddBreakPointServices();

// Add DbContext with MySQL
builder.AddMySqlDbContext<BreakPointDbContext>("BreakPointDb");

// Add Identity
builder
    .Services.AddIdentity<AppUser, IdentityRole<int>>(options =>
    {
        // User settings
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<BreakPointDbContext>()
    .AddDefaultTokenProviders();

builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    });

builder.Services.AddAuthorization();

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Use CORS
app.UseCors("AllowAngularApp");

// Add Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Add security headers
app.Use(
    async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        await next();
    }
);

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BreakPointDbContext>();
    context.Database.EnsureCreated();
    //if (app.Environment.IsDevelopment())
    //{
    //    await Seeder.Seed(context);
    //}
}

// Map endpoints from separate files
app.MapAuthEndpoints();
app.MapPlayerEndpoints();
app.MapMatchEndpoints();
app.MapSwingAnalysisEndpoints();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .WithSummary("Check API health");

app.MapDefaultEndpoints();

app.Run();
