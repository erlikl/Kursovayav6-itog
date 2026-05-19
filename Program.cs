using BackEnd;
using BackEnd.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Set culture to English to ensure English validation messages
var cultureInfo = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// Ensure UTF-8 encoding for console and other outputs
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Add controllers with JSON options for reference handling
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
        // Enable UTF-8 encoding for proper character support
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Configure JSON formatter options
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

// API documentation with Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>  
{
    // Add Bearer authentication scheme
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    // Add security requirement for all endpoints
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
    // Register operation filter to render file inputs for IFormFile parameters
    options.OperationFilter<BackEnd.Swagger.FileUploadOperation>();
});

// Register database context
builder.Services.AddDbContext<ApplicationContext>();

// CORS configuration - allow requests from client app
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
var ClientAppCors = "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins(ClientAppCors)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
});

// Configure JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = AuthOptions.ISSUER,
            ValidateAudience = true,
            ValidAudience = AuthOptions.AUDIENCE,
            ValidateLifetime = true,
            IssuerSigningKey = AuthOptions.GetSymmetricSecurityKey(),
            ValidateIssuerSigningKey = true,
        };
    });

// Register background services
builder.Services.AddHostedService<BackEnd.Services.NotificationsService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Convenience route to open UI
app.MapGet("/ui", ctx =>
{
    ctx.Response.Redirect("/index.html");
    return Task.CompletedTask;
});

app.UseHttpsRedirection();
app.UseCors(MyAllowSpecificOrigins);

// Add exception handling and culture middleware
app.Use(async (context, next) =>
{
    var cultureInfo = new CultureInfo("en-US");
    CultureInfo.CurrentCulture = cultureInfo;
    CultureInfo.CurrentUICulture = cultureInfo;
    System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;
    System.Threading.Thread.CurrentThread.CurrentUICulture = cultureInfo;

    try
    {
        await next();
    }
    catch (Exception ex)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 500;
            var error = new { error = "Internal server error: " + ex.Message };
            await context.Response.WriteAsJsonAsync(error);
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
