using NotificationService.Events;
using RabbitMQ.Client;
using Serilog;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq("http://seq:5341")
    .Enrich.WithProperty("ServiceName", "NotificationService")
    .Enrich.WithProperty("ContainerId", Environment.MachineName)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Redis
    var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "redis:6379";
    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

    // RabbitMQ Connection
    builder.Services.AddSingleton<IConnection>(sp =>
    {
        var factory = new ConnectionFactory
        {
            HostName = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq",
            Port = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
            Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
        };
        return factory.CreateConnectionAsync().GetAwaiter().GetResult();
    });

    builder.Services.AddHostedService<NotificationConsumer>();

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
