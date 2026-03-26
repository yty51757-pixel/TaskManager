using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using TaskManager;
using TaskManager.Auth;
using TaskManager.Data;

var builder = WebApplication.CreateBuilder(args);

// daha once log mekanizmasi kurmadigim icin videolardan ve yapay zekadan yardim aldim 
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft",LogEventLevel.Warning)
    .MinimumLevel.Override("System",LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
        path:"logs/log-.txt",
        rollingInterval:RollingInterval.Day,
        retainedFileCountLimit:30
    )
    .CreateLogger();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers() 
    .AddJsonOptions(x => x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// status, role gibi enum ayarlanmis degerler int olarak degil de string olarak frontendden istenebilsin diye eklendi
builder.Services.AddExceptionHandler<GLobalExceptionHandler>().AddProblemDetails();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt_Token:Issuer"],
            ValidAudience = builder.Configuration["Jwt_Token:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt_Token:SecretKey"] ?? string.Empty)),
            ClockSkew = TimeSpan.Zero
        };
    });
var rawConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection")
                          ?? builder.Configuration.GetConnectionString("PostgresConnection")
                          ?? string.Empty;

if (rawConnectionString.StartsWith("postgresql://") || rawConnectionString.StartsWith("postgres://"))
{
    var uri = new Uri(rawConnectionString);
    var userInfo = uri.UserInfo.Split(':');
    var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty,
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
    };
    rawConnectionString = npgsqlBuilder.ConnectionString;
}

builder.Services.AddDbContext<ApiDbContext>(
    options => options.UseNpgsql(rawConnectionString));
builder.Services.AddSingleton<JwtTokenHelper>();
builder.Host.UseSerilog();

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.Run();
Log.CloseAndFlush();
