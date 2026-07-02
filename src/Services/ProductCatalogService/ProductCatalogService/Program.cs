using MongoDB.Driver;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq("http://seq:5341")
    .Enrich.WithProperty("ServiceName", "ProductCatalogService")
    .Enrich.WithProperty("ContainerId", Environment.MachineName)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // MongoDB
    var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"] ?? "mongodb://mongo:27017";
    var mongoDatabaseName = builder.Configuration["MongoDB:Database"] ?? "productcatalog";
    builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabaseName));

    // Redis Cache
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration["Redis:ConnectionString"] ?? "redis:6379";
        options.InstanceName = "productcatalog:";
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapControllers();
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
