using System.IdentityModel.Tokens.Jwt;
using System.Text;
using MeetingRoomBooking.Api.Configuration;
using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Infrastructure.Persistence.Seed;
using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Auth.Jwt;
using MeetingRoomBooking.Api.Modules.Auth.Seed;
using MeetingRoomBooking.Api.Modules.Resources;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = CorsConfiguration.ValidateAllowedOrigins(
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>());

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsConfiguration.PolicyName, policy =>
    {
        policy
            .WithOrigins([.. allowedOrigins])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddOpenApi();

var connectionString = ConnectionStringValidation.ValidateConnectionString(
    builder.Configuration.GetConnectionString("Default"));
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

var jwtOptions = JwtOptionsValidation.Validate(new JwtOptions
{
    SigningKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty,
    Issuer = builder.Configuration["Jwt:Issuer"] ?? string.Empty,
    Audience = builder.Configuration["Jwt:Audience"] ?? string.Empty,
    AccessTokenLifetimeMinutes = builder.Configuration.GetValue("Jwt:AccessTokenLifetimeMinutes", 15),
    RefreshTokenLifetimeDays = builder.Configuration.GetValue("Jwt:RefreshTokenLifetimeDays", 14),
});
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            RoleClaimType = JwtTokenService.RoleClaimType,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Roles.Admin, policy => policy.RequireRole(Roles.Admin))
    .AddPolicy(Roles.User, policy => policy.RequireRole(Roles.User, Roles.Admin));

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/openapi/v1.json", "Meeting Room Booking API v1");
    });
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(CorsConfiguration.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("Healthy")))
    .WithName("GetHealth")
    .WithOpenApi();

app.MapAuthEndpoints(allowedOrigins);
app.MapResourceEndpoints();
app.MapAdminResourceEndpoints();

// Production migration/seeding is a deliberate deploy-time step (not yet built; this
// stage has no deployment pipeline). Development auto-applies so `dotnet run` and the
// test host always start from a known, migrated, seeded schema.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await RoleSeeder.SeedAsync(roleManager);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await AdminSeeder.SeedAsync(userManager, app.Configuration, app.Logger);

    await DevelopmentDataSeeder.SeedAsync(dbContext);
}

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program;
