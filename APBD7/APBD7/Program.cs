using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WarehouseApp2.Exceptions;
using WarehouseApp2.Middlewares;
using WarehouseApp2.Repositories;
using WarehouseApp2.RequestModels;
using WarehouseApp2.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<SqlConnection>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection")!;
    return new SqlConnection(connectionString);
});
builder.Services.AddScoped<IUnitOfWork>(sp =>
{
    var sqlConnection = sp.GetRequiredService<SqlConnection>();
    return new UnitOfWork(sqlConnection);
});
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseExceptionHandler(new ExceptionHandlerOptions()
{
    AllowStatusCode404Response = true,
    ExceptionHandlingPath = "/error"
});

app.UseHttpsRedirection();

app.Run();