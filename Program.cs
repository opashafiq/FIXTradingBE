using TradingBackend.Api.Fix;
using TradingBackend.Api.Hubs;
using TradingBackend.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using TradingBackend.Api.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi;
using TradingBackend.Api.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("TradingDb")
    )
);

builder.Services.AddDataProtection();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // User settings
        options.User.RequireUniqueEmail = true;

        // Password settings
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Account lockout settings
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<TradingDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();


var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET");

if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "JWT_SECRET environment variable is not configured.");
}

var jwtKey = Convert.FromBase64String(jwtSecret);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(jwtKey),

            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userManager = context.HttpContext.RequestServices
                    .GetRequiredService<UserManager<ApplicationUser>>();

                var userId = context.Principal?
                    .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?
                    .Value;

                if (string.IsNullOrWhiteSpace(userId))
                {
                    context.Fail("Invalid authentication token.");
                    return;
                }

                var user = await userManager.FindByIdAsync(userId);

                if (user == null || !user.IsActive)
                {
                    context.Fail("Your account is inactive or no longer exists.");
                }
            }
        };
    });

builder.Services.AddAuthorization();


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
builder.Services.AddSwaggerGen(options =>
{
    // JWT Bearer Authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token here."
    });

    // Apply Bearer authentication to API operations
    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

builder.Services.AddSingleton<FixMessageDispatcher>();
builder.Services.AddSingleton<ExecutionReportParser>();
builder.Services.AddSingleton<FixApplication>();
builder.Services.AddHostedService<FixSessionManager>();
builder.Services.AddSingleton<
    IIncomingFixMessageQueue,
    IncomingFixMessageQueue>();

builder.Services.AddSingleton<
    IOutgoingFixMessageQueue,
    OutgoingFixMessageQueue>();    
builder.Services.AddHostedService<IncomingFixMessageWorker>();   
builder.Services.AddScoped<ExecutionReportPersistenceService>(); 
builder.Services.AddScoped<FixMessageProcessor>();
builder.Services.AddHostedService<FixMessageRecoveryWorker>();
builder.Services.AddHostedService<OutgoingFixMessageWorker>();
builder.Services.AddSingleton<FixOrderSubmissionService>();
builder.Services.AddHostedService<IdentitySeedHostedService>();
builder.Services.AddScoped<BusinessMessageRejectPersistenceService>();
builder.Services.AddScoped<OrderCancelRejectPersistenceService>();
builder.Services.AddScoped<OrderCancellationService>();
builder.Services.AddScoped<OrderReplacementService>();
builder.Services.AddScoped<SessionRejectPersistenceService>();


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



app.UseCors("SignalRPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<FixStatusHub>("/hubs/fix-status");

// Seed Identity roles
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    await IdentitySeeder.SeedRolesAsync(roleManager);
}

app.Run();
