using MeetingRoomBooking.Api.Configuration;
using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Infrastructure.Persistence.Seed;
using MeetingRoomBooking.Api.Modules.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

app.MapOpenApi();
app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("Healthy")))
    .WithName("GetHealth")
    .WithOpenApi();

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

    await DevelopmentDataSeeder.SeedAsync(dbContext);
}

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program;
