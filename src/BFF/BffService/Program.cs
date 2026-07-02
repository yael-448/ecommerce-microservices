var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClient for each downstream service
builder.Services.AddHttpClient("OrderService", c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:OrderService"] ?? "http://order-service:8080"));

builder.Services.AddHttpClient("ProductCatalogService", c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:ProductCatalogService"] ?? "http://product-catalog-1:8080"));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
