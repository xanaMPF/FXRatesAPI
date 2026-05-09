using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Configuration;
using Microsoft.EntityFrameworkCore;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        // Register AppDbContext using connection string from settings (default to Sqlite for local dev)
        var cs = ctx.Configuration.GetConnectionString("AppDb") ?? ctx.Configuration["AppDb__ConnectionString"];
        if (!string.IsNullOrEmpty(cs))
        {
            // Use Sqlite by default to match the API project local dev setup; change to UseSqlServer if needed in production.
            services.AddDbContext<FxRatesApi.Api.Infrastructure.Persistence.AppDbContext>(options =>
                options.UseSqlite(cs));
        }
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.AddConsole();
    })
    .Build();

host.Run();
