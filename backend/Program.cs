using backend.Hubs;
using backend.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// SignalR — real-time streaming to browser
builder.Services.AddSignalR();

// Redis — rate limiting
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));

// App services
builder.Services.AddScoped<RateLimitService>();
builder.Services.AddScoped<ExecutionService>();
builder.Services.AddScoped<AIReviewService>();

// Allow Next.js frontend to call this API
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()); // required for SignalR
});

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();
app.MapHub<ExecutionHub>("/hubs/execution");

app.Run();
