using MeetingRoomBooking.Api.Configuration;

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

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program;
