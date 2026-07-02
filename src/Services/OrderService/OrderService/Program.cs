using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Events;
using RabbitMQ.Client;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq("http://seq:5341")
    .Enrich.WithProperty("ServiceName", "OrderService")
    .Enrich.WithProperty("ContainerId", Environment.MachineName)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // PostgreSQL
    builder.Services.AddDbContext<OrderDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

    builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();
    builder.Services.AddHostedService<OrderSagaConsumer>();

    var app = builder.Build();

    // Auto-migrate
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Database.Migrate();
    }

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
