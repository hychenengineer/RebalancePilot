using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TamaracCoPilot.Api.Application.Behaviors;
using TamaracCoPilot.Api.Application.Services;
using TamaracCoPilot.Api.Infrastructure.Data;
using TamaracCoPilot.Api.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Database & Persistence (EF Core SQLite)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=tamarac.db";
builder.Services.AddDbContext<TamaracDbContext>(options =>
    options.UseSqlite(connectionString));

// 2. Repositories (Controller -> Service -> Repository Pattern)
builder.Services.AddScoped<IPortfolioRepository, PortfolioRepository>();
builder.Services.AddScoped<ITradeProposalRepository, TradeProposalRepository>();

// 3. Domain & AI Application Services
builder.Services.AddScoped<IRebalanceService, RebalanceService>();
builder.Services.AddScoped<IAdvisorCoPilotService, AdvisorCoPilotService>();

// 4. MediatR CQRS & Pipeline Behaviors
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// 5. FluentValidation Auto-Discovery
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// 6. Controllers & JSON Options
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// 7. Kubernetes / Cloud Readiness: Health Checks
builder.Services.AddHealthChecks();

// 8. OpenAPI / Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate & seed demo data on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TamaracDbContext>();
    await DbInitializer.SeedAsync(context);
}

// HTTP Pipeline
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tamarac Co-Pilot API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/healthz");
app.MapControllers();

app.Run();

// For integration testing
public partial class Program { }
