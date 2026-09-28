using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using System.Text.Json.Serialization;
using eZakazivanje.DataService.BackgroundServices;
using eZakazivanje.Entity.Settings;
using Microsoft.Extensions.Options;
using eZakazivanje.DataService.BackgroundServices.Services;
using Microsoft.Extensions.FileProviders;
using eZakazivanje.DataService.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using eZakazivanje.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "eZakazivanje API", 
        Version = "v1",
        Description = "API for eZakazivanje application"
    });

    // Configure JWT authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});

builder.Services.AddEndpointsApiExplorer();

// Configure DbContext
// In development, use appsettings.Development.json; in production, use environment variables
var connectionString = string.Empty;
if (builder.Environment.IsDevelopment())
{
    // Use connection string from appsettings.Development.json
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection string is not configured in appsettings.Development.json");
}
else
{
    // Use environment variables for production/Docker
    var dbHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") 
        ?? throw new InvalidOperationException("Database host environment variable (POSTGRES_HOST) is not configured");
    var dbName = Environment.GetEnvironmentVariable("POSTGRES_DB") 
        ?? throw new InvalidOperationException("Database name environment variable (POSTGRES_DB) is not configured");
    var dbUser = Environment.GetEnvironmentVariable("POSTGRES_USER") 
        ?? throw new InvalidOperationException("Database user environment variable (POSTGRES_USER) is not configured");
    var dbPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") 
        ?? throw new InvalidOperationException("Database password environment variable (POSTGRES_PASSWORD) is not configured");

    connectionString = $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPassword}";
}

builder.Services.AddDbContext<AppDbContext>(options => 
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    });
});

// Configure Logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    logging.AddProvider(new FileLoggerProvider("/app/logs/app.log"));
});

// Force console output to be unbuffered
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("Application starting - logging test");

// Register ILogger<T> as Singleton
builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

// Register repositories and services
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IBussinessRepository>(provider =>
{
    var context = provider.GetRequiredService<AppDbContext>();
    var logger = provider.GetRequiredService<ILogger<BussinessRepository>>();
    return new BussinessRepository(context, logger);
});

// Add UserRepository registration
builder.Services.AddScoped<IUserRepository>(provider =>
{
    var context = provider.GetRequiredService<AppDbContext>();
    var logger = provider.GetRequiredService<ILogger<UserRepository>>();
    return new UserRepository(context, logger);
});

builder.Services.AddTransient<IAuthService, AuthService>();

// For Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false; // Don't require special characters
    options.Password.RequiredLength = 8; // Minimum 8 characters
    options.Password.RequiredUniqueChars = 0; // No unique character requirement
    
    // Lockout settings (optional)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    
    // User settings
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Check JWT configuration before building the app
// In development, use appsettings.Development.json; in production, use environment variables
var jwtSecret = string.Empty;
var jwtValidIssuer = string.Empty;
var jwtValidAudience = string.Empty;

if (builder.Environment.IsDevelopment())
{
    // Use JWT settings from appsettings.Development.json
    jwtSecret = builder.Configuration["JWT:Secret"]
        ?? throw new InvalidOperationException("JWT Secret is not configured in appsettings.Development.json");
    jwtValidIssuer = builder.Configuration["JWT:ValidIssuer"] ?? "https://zakazime.sliplane.app";
    jwtValidAudience = builder.Configuration["JWT:ValidAudience"] ?? "https://zakazime.sliplane.app";
}
else
{
    // Use environment variables for production
    jwtSecret = Environment.GetEnvironmentVariable("JWT__Secret")
        ?? throw new InvalidOperationException("JWT Secret environment variable (JWT__Secret) is not configured");
    jwtValidIssuer = Environment.GetEnvironmentVariable("JWT__ValidIssuer") ?? "https://zakazime.sliplane.app";
    jwtValidAudience = Environment.GetEnvironmentVariable("JWT__ValidAudience") ?? "https://zakazime.sliplane.app";
}

var jwtSettings = new
{
    ValidIssuer = jwtValidIssuer,
    ValidAudience = jwtValidAudience,
    Secret = jwtSecret
};

// Configure JWT settings for the application
builder.Services.Configure<JwtSettings>(options =>
{
    options.Secret = jwtSettings.Secret;
    options.ValidAudience = jwtSettings.ValidAudience;
    options.ValidIssuer = jwtSettings.ValidIssuer;
    options.TokenExpiryTimeInHours = 24;
});

// Authentication configuration
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.ValidIssuer,
        ValidAudience = jwtSettings.ValidAudience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            return Task.CompletedTask;
        }
    };
});

// Conditionally add OAuth providers only when configured
bool googleConfigured;
bool facebookConfigured;
bool microsoftConfigured;

if (builder.Environment.IsDevelopment())
{
    googleConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["OAuth:Google:ClientId"]) &&
                       !string.IsNullOrWhiteSpace(builder.Configuration["OAuth:Google:ClientSecret"]);
    facebookConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["OAuth:Facebook:AppId"]) &&
                         !string.IsNullOrWhiteSpace(builder.Configuration["OAuth:Facebook:AppSecret"]);
    microsoftConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["OAuth:Microsoft:ClientId"]) &&
                         !string.IsNullOrWhiteSpace(builder.Configuration["OAuth:Microsoft:ClientSecret"]);
}
else
{
    googleConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OAUTH__GOOGLE__CLIENT_ID")) &&
                       !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OAUTH__GOOGLE__CLIENT_SECRET"));
    facebookConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OAUTH__FACEBOOK__APP_ID")) &&
                         !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OAUTH__FACEBOOK__APP_SECRET"));
    microsoftConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OAUTH__MICROSOFT__CLIENT_ID")) &&
                         !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OAUTH__MICROSOFT__CLIENT_SECRET"));
}

if (googleConfigured)
{
    authBuilder.AddGoogle(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.ClientId = builder.Configuration["OAuth:Google:ClientId"] ?? "";
            options.ClientSecret = builder.Configuration["OAuth:Google:ClientSecret"] ?? "";
        }
        else
        {
            options.ClientId = Environment.GetEnvironmentVariable("OAUTH__GOOGLE__CLIENT_ID") ?? "";
            options.ClientSecret = Environment.GetEnvironmentVariable("OAUTH__GOOGLE__CLIENT_SECRET") ?? "";
        }
        options.CallbackPath = "/api/authentication/signin-google";
    });
}
else
{
    Console.WriteLine("Warning: Google OAuth not configured. Social login with Google will not be available.");
}

if (facebookConfigured)
{
    authBuilder.AddFacebook(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.AppId = builder.Configuration["OAuth:Facebook:AppId"] ?? "";
            options.AppSecret = builder.Configuration["OAuth:Facebook:AppSecret"] ?? "";
        }
        else
        {
            options.AppId = Environment.GetEnvironmentVariable("OAUTH__FACEBOOK__APP_ID") ?? "";
            options.AppSecret = Environment.GetEnvironmentVariable("OAUTH__FACEBOOK__APP_SECRET") ?? "";
        }
        options.CallbackPath = "/api/authentication/signin-facebook";
    });
}
else
{
    Console.WriteLine("Warning: Facebook OAuth not configured. Social login with Facebook will not be available.");
}

if (microsoftConfigured)
{
    authBuilder.AddMicrosoftAccount(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.ClientId = builder.Configuration["OAuth:Microsoft:ClientId"] ?? "";
            options.ClientSecret = builder.Configuration["OAuth:Microsoft:ClientSecret"] ?? "";
        }
        else
        {
            options.ClientId = Environment.GetEnvironmentVariable("OAUTH__MICROSOFT__CLIENT_ID") ?? "";
            options.ClientSecret = Environment.GetEnvironmentVariable("OAUTH__MICROSOFT__CLIENT_SECRET") ?? "";
        }
        options.CallbackPath = "/api/authentication/signin-microsoft";
    });
}
else
{
    Console.WriteLine("Warning: Microsoft OAuth not configured. Social login with Microsoft will not be available.");
}

// Note: Apple Sign In uses ID token verification (handled in AuthService)
// It doesn't require OAuth callback configuration like Google/Facebook

// Add authorization after authentication
builder.Services.AddAuthorization();

// Update the CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("Open", builder =>
        builder
            .SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// Add before services configuration
// In development, read Firebase credentials from file path; in production, use environment variable
var credentialsPath = string.Empty;
var firebaseCredentials = string.Empty;

if (builder.Environment.IsDevelopment())
{
    // In development, use the file path from appsettings.Development.json
    var firebaseCredentialsPath = builder.Configuration["Firebase:Credentials"];
    if (string.IsNullOrEmpty(firebaseCredentialsPath))
    {
        throw new InvalidOperationException("Firebase credentials path is not configured in appsettings.Development.json");
    }

    credentialsPath = firebaseCredentialsPath;
    
    // Check if file exists
    if (!File.Exists(credentialsPath))
    {
        throw new InvalidOperationException($"Firebase credentials file not found at: {credentialsPath}");
    }

    // Read credentials from file
    firebaseCredentials = File.ReadAllText(credentialsPath);
    
    if (string.IsNullOrEmpty(firebaseCredentials))
    {
        throw new InvalidOperationException("Firebase credentials file is empty");
    }
}
else
{
    // In production, use environment variable and write to file
    var firebaseSettings = builder.Configuration.GetSection("Firebase").Get<FirebaseSettings>();
    if (firebaseSettings == null)
    {
        throw new InvalidOperationException("Firebase settings are not configured");
    }

    credentialsPath = Path.Combine(AppContext.BaseDirectory, "config", firebaseSettings.CredentialsFile);
    firebaseCredentials = builder.Configuration["FIREBASE_CREDENTIALS"] ?? string.Empty;

    if (string.IsNullOrEmpty(firebaseCredentials))
    {
        throw new InvalidOperationException("Firebase credentials environment variable is not configured");
    }

    // Create the config directory if it doesn't exist
    var configDir = Path.GetDirectoryName(credentialsPath);
    if (configDir != null && !Directory.Exists(configDir))
    {
        Directory.CreateDirectory(configDir);
    }

    // Write the credentials to file
    File.WriteAllText(credentialsPath, firebaseCredentials);
}

// Validate that the credentials are valid JSON
try
{
    var jsonDoc = System.Text.Json.JsonDocument.Parse(firebaseCredentials);
    if (jsonDoc.RootElement.GetProperty("type").GetString() != "service_account")
    {
        throw new InvalidOperationException("Firebase credentials are not in the correct format");
    }
}
catch (Exception ex)
{
    throw new InvalidOperationException("Firebase credentials are not valid JSON", ex);
}

// Verify the file exists
if (!File.Exists(credentialsPath))
{
    throw new InvalidOperationException($"Failed to access Firebase credentials file at {credentialsPath}");
}

try
{
    
    FirebaseApp.Create(new AppOptions()
    {
        Credential = GoogleCredential.FromFile(credentialsPath)
    });
    Console.WriteLine("Firebase initialized successfully");
}
catch (Exception ex)
{
    throw new InvalidOperationException("Failed to initialize Firebase", ex);
}

// Add this after JWT configuration
builder.Services.AddHostedService<AppointmentReminderService>();
builder.Services.AddHostedService<SubscriptionVerificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<EmailService>();

// Add Firebase configuration (after JWT settings)
builder.Services.Configure<FirebaseSettings>(builder.Configuration.GetSection("Firebase"));

// Update email configuration
// In development, email is optional; in production, use environment variables
if (builder.Environment.IsDevelopment())
{
    // Email is optional in development - use defaults or skip if not configured
    builder.Services.Configure<EmailSettings>(options =>
    {
        options.SmtpServer = builder.Configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
        options.SmtpPort = int.Parse(builder.Configuration["Email:SmtpPort"] ?? "587");
        options.Username = builder.Configuration["Email:Username"] ?? "";
        options.Password = builder.Configuration["Email:Password"] ?? "";
        options.SenderEmail = builder.Configuration["Email:SenderEmail"] ?? "";
        options.SenderName = builder.Configuration["Email:SenderName"] ?? "eZakazivanje";
    });
}
else
{
    // Production: require environment variables
    string emailPassword = Environment.GetEnvironmentVariable("Email__Password")
        ?? throw new InvalidOperationException("Email password environment variable (Email__Password) is not configured");

    builder.Services.Configure<EmailSettings>(options =>
    {
        options.SmtpServer = Environment.GetEnvironmentVariable("Email__SmtpServer")
            ?? throw new InvalidOperationException("SMTP Server environment variable (Email__SmtpServer) is not configured");
        options.SmtpPort = int.Parse(
            Environment.GetEnvironmentVariable("Email__SmtpPort") ?? "587");
        options.Username = Environment.GetEnvironmentVariable("Email__Username")
            ?? throw new InvalidOperationException("SMTP Username environment variable (Email__Username) is not configured");
        options.Password = emailPassword;
        options.SenderEmail = Environment.GetEnvironmentVariable("Email__SenderEmail")
            ?? throw new InvalidOperationException("Sender Email environment variable (Email__SenderEmail) is not configured");
        options.SenderName = Environment.GetEnvironmentVariable("Email__SenderName") ?? "Termino";
    });
}

builder.Services.AddScoped<IFileService, FileService>();

// Tokens for business approval links (email click)
var dataProtectionKeysPath =
    Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "dpkeys");

Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("eZakazivanje");
builder.Services.AddScoped<eZakazivanje.DataService.Services.BusinessApprovalLinkService>();

// Configure Google Play settings
// In development, use appsettings.Development.json; in production, use environment variables
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<GooglePlaySettings>(builder.Configuration.GetSection("GooglePlay"));
}
else
{
    // Production: use environment variables
    var googlePlaySettings = new GooglePlaySettings
    {
        ServiceAccountKeyPath = Environment.GetEnvironmentVariable("GOOGLE_PLAY__SERVICE_ACCOUNT_KEY_PATH")
            ?? "config/google-play-service-account.json",
        PackageName = Environment.GetEnvironmentVariable("GOOGLE_PLAY__PACKAGE_NAME")
            ?? throw new InvalidOperationException("GOOGLE_PLAY__PACKAGE_NAME environment variable is not configured")
    };
    builder.Services.Configure<GooglePlaySettings>(options =>
    {
        options.ServiceAccountKeyPath = googlePlaySettings.ServiceAccountKeyPath;
        options.PackageName = googlePlaySettings.PackageName;
    });

    // Write Google Play credentials from environment variable to file (if provided)
    var googlePlayCredentials = Environment.GetEnvironmentVariable("GOOGLE_PLAY_CREDENTIALS");
    if (!string.IsNullOrEmpty(googlePlayCredentials))
    {
        var googlePlayCredentialsPath = Path.Combine(AppContext.BaseDirectory, googlePlaySettings.ServiceAccountKeyPath);
        var googlePlayConfigDir = Path.GetDirectoryName(googlePlayCredentialsPath);
        if (googlePlayConfigDir != null && !Directory.Exists(googlePlayConfigDir))
        {
            Directory.CreateDirectory(googlePlayConfigDir);
        }
        File.WriteAllText(googlePlayCredentialsPath, googlePlayCredentials);
        Console.WriteLine("Google Play credentials written to file from environment variable");
    }
}

// Configure Apple settings
// In development, use appsettings.Development.json; in production, use environment variables
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<AppleSettings>(builder.Configuration.GetSection("Apple"));
}
else
{
    // Production: use environment variables
    var appleSettings = new AppleSettings
    {
        KeyId = Environment.GetEnvironmentVariable("APPLE__KEY_ID")
            ?? throw new InvalidOperationException("APPLE__KEY_ID environment variable is not configured"),
        IssuerId = Environment.GetEnvironmentVariable("APPLE__ISSUER_ID")
            ?? throw new InvalidOperationException("APPLE__ISSUER_ID environment variable is not configured"),
        BundleId = Environment.GetEnvironmentVariable("APPLE__BUNDLE_ID")
            ?? throw new InvalidOperationException("APPLE__BUNDLE_ID environment variable is not configured"),
        PrivateKeyPath = Environment.GetEnvironmentVariable("APPLE__PRIVATE_KEY_PATH")
            ?? "config/apple-auth-key.p8",
        // Production: use Apple production StoreKit API by default (set APPLE__USE_SANDBOX=true only for sandbox testing)
        UseSandbox = string.Equals(Environment.GetEnvironmentVariable("APPLE__USE_SANDBOX"), "true", StringComparison.OrdinalIgnoreCase)
    };
    builder.Services.Configure<AppleSettings>(options =>
    {
        options.KeyId = appleSettings.KeyId;
        options.IssuerId = appleSettings.IssuerId;
        options.BundleId = appleSettings.BundleId;
        options.PrivateKeyPath = appleSettings.PrivateKeyPath;
        options.UseSandbox = appleSettings.UseSandbox;
    });

    // Write Apple private key from environment variable to file (if provided)
    var applePrivateKey = Environment.GetEnvironmentVariable("APPLE_PRIVATE_KEY");
    if (!string.IsNullOrEmpty(applePrivateKey))
    {
        var appleKeyPath = Path.Combine(AppContext.BaseDirectory, appleSettings.PrivateKeyPath);
        var appleConfigDir = Path.GetDirectoryName(appleKeyPath);
        if (appleConfigDir != null && !Directory.Exists(appleConfigDir))
        {
            Directory.CreateDirectory(appleConfigDir);
        }
        File.WriteAllText(appleKeyPath, applePrivateKey);
        Console.WriteLine("Apple private key written to file from environment variable");
    }
}

// Register subscription services
builder.Services.AddScoped<eZakazivanje.DataService.Services.Interfaces.IGooglePlayBillingService, eZakazivanje.DataService.Services.GooglePlayBillingService>();
builder.Services.AddScoped<eZakazivanje.DataService.Services.Interfaces.IAppleAppStoreService, eZakazivanje.DataService.Services.AppleAppStoreService>();
builder.Services.AddScoped<eZakazivanje.DataService.Services.Interfaces.ISubscriptionService, eZakazivanje.DataService.Services.SubscriptionService>();
builder.Services.AddHttpClient<eZakazivanje.DataService.Services.AppleAppStoreService>();

// Ensure wwwroot directory exists
var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var uploadsPath = Path.Combine(webRootPath, "uploads");

if (!Directory.Exists(webRootPath))
{
    Directory.CreateDirectory(webRootPath);
}

if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

// Configure static files with the correct path
builder.Services.Configure<StaticFileOptions>(options =>
{
    options.FileProvider = new PhysicalFileProvider(webRootPath);
    options.ServeUnknownFileTypes = true; // Allow serving files without extensions
    options.DefaultContentType = "application/octet-stream";
});

// Add before builder.Build()
builder.Services.AddHealthChecks();

// Update the Kestrel configuration
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    serverOptions.AllowSynchronousIO = true;
    
    if (builder.Environment.IsDevelopment())
    {
        // When running locally without Docker
        serverOptions.ListenAnyIP(5000);
    }
    else
    {
        // When running in Docker
        serverOptions.ListenAnyIP(80);
    }
});

var app = builder.Build();

// Add a test log to verify logging is working
app.Logger.LogInformation("Application starting up - Logging test");
app.Logger.LogWarning("This is a test warning log");
app.Logger.LogError("This is a test error log");

// Force immediate console output
Console.WriteLine("=== APPLICATION STARTING ===");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"Content Root: {app.Environment.ContentRootPath}");
Console.WriteLine("=== END STARTUP INFO ===");


try
{
    // Apply migrations with retry logic
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        
        app.Logger.LogInformation("Applying database migrations...");
        
        var retryCount = 0;
        var maxRetries = 3;
        var delay = TimeSpan.FromSeconds(5);
        
        while (retryCount < maxRetries)
        {
            try
            {
                dbContext.Database.Migrate();
                app.Logger.LogInformation("Database migrations applied successfully");
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount == maxRetries)
                {
                    app.Logger.LogError(ex, "Failed to apply migrations after {RetryCount} attempts", maxRetries);
                    throw;
                }
                app.Logger.LogWarning(ex, "Migration attempt {RetryCount} failed, retrying in {Delay} seconds...", retryCount, delay.TotalSeconds);
                Thread.Sleep(delay);
                delay *= 2; // Exponential backoff
            }
        }

        // Create admin role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            app.Logger.LogInformation("Admin role created successfully");
        }

        // Check if admin user exists
        var adminEmail = builder.Configuration["AdminUser:Email"] ?? "stefanvasic991@gmail.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            // Create admin user
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true, // Set email as confirmed
                FirstName = "Admin",
                LastName = "User",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var adminPassword = builder.Configuration["AdminUser:Password"] ?? "Admin123!";
            var result = await userManager.CreateAsync(adminUser, adminPassword);

            if (result.Succeeded)
            {
                // Add admin role to user
                await userManager.AddToRoleAsync(adminUser, "Admin");
                app.Logger.LogInformation("Admin user created successfully");
            }
            else
            {
                app.Logger.LogError("Failed to create admin user: {Errors}", 
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            app.Logger.LogInformation("Admin user already exists");
        }
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "An error occurred while applying migrations or creating admin user");
    throw;
}

// Move Swagger outside of Development check
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "eZakazivanje API V1");
    c.RoutePrefix = "swagger";
});

// Update middleware order
app.UseRouting();
app.UseCors("Open");
// Health check should be accessible without authentication
app.UseHealthChecks("/health");
app.UseAuthentication();
app.UseAuthorization();

// Add CORS headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
    context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
    context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
    context.Response.Headers.Append("Connection", "keep-alive");
    
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        return;
    }
    
    await next();
});

// Root endpoint for Sliplane health check
app.MapGet("/", () => Results.Ok(new { status = "OK", message = "eZakazivanje API is running" }));

app.MapControllers();
app.UseStaticFiles();


app.Run();
