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


var fixEnabled = builder.Configuration.GetValue(
    "Fix:Enabled",
    true);



if (fixEnabled)
{
    builder.Services.AddHostedService<FixSessionManager>();
    builder.Services.AddHostedService<IncomingFixMessageWorker>();
    builder.Services.AddHostedService<FixMessageRecoveryWorker>();
    builder.Services.AddHostedService<OutgoingFixMessageWorker>();
    builder.Services.AddHostedService<QuoteExecutionReconciliationWorker>();
}
else
{
    // Development testing:
    // Process synthetic inbound FIX messages without connecting to CHSE.
    builder.Services.AddHostedService<IncomingFixMessageWorker>();
}

builder.Services.AddSingleton<
    IIncomingFixMessageQueue,
    IncomingFixMessageQueue>();

builder.Services.AddSingleton<
    IOutgoingFixMessageQueue,
    OutgoingFixMessageQueue>();    
   
builder.Services.AddScoped<ExecutionReportPersistenceService>(); 
builder.Services.AddScoped<FixMessageProcessor>();


builder.Services.AddSingleton<FixOrderSubmissionService>();
builder.Services.AddSingleton<FixQuoteSubmissionService>();
builder.Services.AddHostedService<IdentitySeedHostedService>();
builder.Services.AddScoped<BusinessMessageRejectPersistenceService>();
builder.Services.AddScoped<OrderCancelRejectPersistenceService>();
builder.Services.AddScoped<OrderCancellationService>();
builder.Services.AddScoped<QuoteExecutionReconciliationService>();

builder.Services.AddScoped<OrderReplacementService>();
builder.Services.AddScoped<SessionRejectPersistenceService>();
builder.Services.AddScoped<QuoteStatusReportPersistenceService>();


var app = builder.Build();

////////////////////////////////
/// Temporary endpointo to test quotes
/// 

// Temporary development-only endpoint for synthetic FIX testing.

// Temporary development-only endpoint for synthetic FIX testing.
if (app.Environment.IsDevelopment())
{
    app.MapPost(
        "/__dev/fix/inbound-execution-report",
        async (
            SyntheticFixMessageRequest request,
            IIncomingFixMessageQueue queue,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.RawMessage))
            {
                return Results.BadRequest(
                    "Raw FIX message is required.");
            }

            if (request.MsgSeqNum <= 0)
            {
                return Results.BadRequest(
                    "MsgSeqNum must be greater than zero.");
            }

            // Preserve the existing test session as the default.
            var sessionQualifier =
                string.IsNullOrWhiteSpace(request.SessionQualifier)
                    ? "TEST-QUOTE-LATE-REF-SESSION-002"
                    : request.SessionQualifier.Trim();

            var sessionId =
                $"FIXT.1.1:CSECOMPN1->FGW:{sessionQualifier}";

            var incomingMessage = new IncomingFixMessage
            {
                SessionId = sessionId,

                BeginString = "FIXT.1.1",
                SenderCompID = "CSECOMPN1",
                TargetCompID = "FGW",

                SessionQualifier = sessionQualifier,

                MsgType = "8",
                ClOrdId = request.ClOrdId,
                RawMessage = request.RawMessage,

                MsgSeqNum = request.MsgSeqNum,
                PossDupFlag = false,
                PossResend = false,

                ReceivedAtUtc = DateTimeOffset.UtcNow
            };

            await queue.EnqueueAsync(
                incomingMessage,
                cancellationToken);

            return Results.Accepted(
                value: new
                {
                    Message = "Synthetic FIX message queued.",
                    SessionId = incomingMessage.SessionId,
                    MsgType = incomingMessage.MsgType,
                    MsgSeqNum = incomingMessage.MsgSeqNum
                });
        });
}



// Temporary endpoint to test quotes ends


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

////////////////////////////////
/// Temporary endpointo to test quotes
/// 

public record SyntheticFixMessageRequest(
    string ClOrdId,
    string RawMessage,
    int MsgSeqNum,
    string? SessionQualifier = null);


//// ENds
