using System.Text;
using FairQueueService.Api.Settings;
using FairQueueService.Application.DTOs;
using FairQueueService.Application.Interfaces;
using FairQueueService.Application.UseCases;
using FairQueueService.Domain.Ports;
using FairQueueService.Infrastructure.Messaging;
using FairQueueService.Infrastructure.Persistence;
using FairQueueService.Infrastructure.Persistence.Repositories;
using FairQueueService.Infrastructure.SSE;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Settings ──────────────────────────────────────────────────────────────────
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection(RabbitMQSettings.SectionName));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<QueueSettings>(builder.Configuration.GetSection(QueueSettings.SectionName));

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<FairQueueDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Domain output ports → Infrastructure adapters ─────────────────────────────
builder.Services.AddScoped<IQueueRepository, QueueRepository>();
builder.Services.AddSingleton<ITicketReservedPublisher, RabbitMQTicketReservedPublisher>();

// ── SSE Hub (Singleton compartido entre requests) ──────────────────────────────
builder.Services.AddSingleton<QueuePositionHub>();
builder.Services.AddSingleton<IQueuePositionNotifier>(sp => sp.GetRequiredService<QueuePositionHub>());
builder.Services.AddSingleton<IQueuePositionSubscriber>(sp => sp.GetRequiredService<QueuePositionHub>());

// ── Application use cases (Scoped) ────────────────────────────────────────────
builder.Services.AddScoped<IEnterQueueUseCase, EnterQueueCommandHandler>();
builder.Services.AddScoped<ILeaveQueueUseCase, LeaveQueueCommandHandler>();
builder.Services.AddScoped<IGetQueuePositionUseCase, GetQueuePositionQueryHandler>();
builder.Services.AddScoped<IAdvanceQueueUseCase, AdvanceQueueCommandHandler>();

// ── Background services ───────────────────────────────────────────────────────
builder.Services.AddHostedService<TicketReleasedConsumer>();
builder.Services.AddHostedService<TurnExpirationWorker>();

// ── JWT — valida tokens emitidos por AuthService ──────────────────────────────
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSection["Secret"] ?? throw new InvalidOperationException("JwtSettings:Secret is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer   = true,
            ValidIssuer      = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience    = jwtSection["Audience"],
            ValidateLifetime = true,
            ClockSkew        = TimeSpan.Zero,
            NameClaimType    = "sub"
        };

        // SSE: EventSource del browser no soporta headers — leer token desde query param
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.Request.Path.StartsWithSegments("/api/queue") &&
                    ctx.Request.Path.Value?.EndsWith("/stream") == true)
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── CORS ──────────────────────────────────────────────────────────────────────
// HUMAN CHECK: AllowAnyOrigin solo para desarrollo — restringir en producción
builder.Services.AddCors(opts =>
    opts.AddPolicy("FrontendPolicy", p =>
        p.WithOrigins("http://localhost:5173", "http://localhost:3000")
         .AllowAnyHeader()
         .AllowAnyMethod()));

// ── Controllers + Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ticketify — FairQueue Service",
        Version = "v1",
        Description = "Cola justa y transparente para acceso a tickets de cualquier evento."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT",
        Description = "JWT del AuthService. Ejemplo: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});

var app = builder.Build();

// ── Auto-migrate en startup (dev) ──────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FairQueue Service v1"));
}

app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Ticketify-FairQueueService" }));

// Aplicar migraciones pendientes al iniciar (evita correr EF CLI manualmente en dev)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FairQueueDbContext>();
    db.Database.EnsureCreated();   // Usa schema.sql vía Docker; solo crea si no existe
}

app.Run();
