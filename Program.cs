using FxRatesApi.Api.Application.Events;
using FxRatesApi.Api.Application.Services;
using FxRatesApi.Api.Domain.Constants;
using FxRatesApi.Api.Infrastructure.Configuration;
using FxRatesApi.Api.Infrastructure.Events;
using FxRatesApi.Api.Infrastructure.Persistence;
using FxRatesApi.Api.Infrastructure.Providers.AlphaVantage;
using FxRatesApi.Api.Presentation.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var alphaVantageApiKey = builder.Configuration[AlphaVantageConstants.ApiKeyEnvironmentVariable];
if (!string.IsNullOrWhiteSpace(alphaVantageApiKey))
{
    builder.Configuration[$"{AlphaVantageOptions.SectionName}:{nameof(AlphaVantageOptions.ApiKey)}"] = alphaVantageApiKey;
}

builder.Services.Configure<AlphaVantageOptions>(
    builder.Configuration.GetSection(AlphaVantageOptions.SectionName));
builder.Services.Configure<ExchangeRateLookupOptions>(
    builder.Configuration.GetSection(ExchangeRateLookupOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=fxrates.db"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<IExchangeRateProvider, AlphaVantageService>();
builder.Services.AddScoped<IExchangeRateResolver, ExchangeRateResolver>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();
builder.Services.AddSingleton<InMemoryRateEventQueue>();
builder.Services.AddSingleton<IRateEventPublisher>(sp => sp.GetRequiredService<InMemoryRateEventQueue>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<InMemoryRateEventQueue>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
