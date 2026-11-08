using TradingBackend.Api.Fix;
using TradingBackend.Api.Hubs;
using TradingBackend.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("TradingDb")
    )
);
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:5500")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// These two registrations are important
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<FixMessageDispatcher>();
builder.Services.AddSingleton<ExecutionReportParser>();
builder.Services.AddSingleton<FixApplication>();
builder.Services.AddHostedService<FixSessionManager>();
builder.Services.AddSingleton<
    IIncomingFixMessageQueue,
    IncomingFixMessageQueue>();
builder.Services.AddHostedService<IncomingFixMessageWorker>();    

var app = builder.Build();

// Enable Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCors("SignalRPolicy");

app.MapControllers();
app.MapHub<FixStatusHub>("/hubs/fix-status");

app.Run();
