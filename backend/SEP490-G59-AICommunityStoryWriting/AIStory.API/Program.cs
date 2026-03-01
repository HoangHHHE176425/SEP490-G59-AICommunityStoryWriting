using AIStory.Services.Helpers;
using AIStory.Services.Implementations;
using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repositories;
using Repositories.Implementations;
using Repositories.Interfaces;
using Services.Implementations;
using Services.Interfaces;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace AIStory.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Load local-only config overrides (secrets) without committing them.
            // Priority: Local files override appsettings.json/appsettings.Development.json.
            builder.Configuration
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: true);

            // =======================
            // Add services
            // =======================

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.WriteIndented = true;
                });
            builder.Services.AddDbContext<StoryPlatformDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection") 
                    ?? "Server= QUANGMANH;uid=sa;password=123;database=story_platform_v13;Encrypt=True;TrustServerCertificate=True;",
                    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null)
                ));
            // CORS Configuration
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowClient", policy =>
                {

                    policy.SetIsOriginAllowed(origin =>
                    {
                        if (string.IsNullOrWhiteSpace(origin)) return false;
                        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                               || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
                    })
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });
            builder.Services.AddScoped<JwtHelper>();
            // Dependency Injection
            // dj for auth va user
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IOtpRepository, OtpRepository>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IEmailService, EmailService>();

            builder.Services.AddScoped<IStoryRepository, StoryRepository>();
            builder.Services.AddScoped<IStoryService, StoryService>();
            builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
            builder.Services.AddScoped<ICategoryService, CategoryService>();
            builder.Services.AddScoped<IChapterRepository, ChapterRepository>();
            builder.Services.AddScoped<IChapterService, ChapterService>();

            builder.Services.AddHttpClient();
            builder.Services.AddScoped<IStoryAiService, StoryAiService>();

            // Policies
            builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
            builder.Services.AddScoped<IAuthorPolicyAcceptanceRepository, AuthorPolicyAcceptanceRepository>();
            builder.Services.AddScoped<IPolicyService, PolicyService>();
            builder.Services.AddScoped<IAdminPolicyService, AdminPolicyService>();
            builder.Services.AddScoped<IAdminUserService, AdminUserService>();
            builder.Services.AddScoped<IModeratorCategoryAssignmentRepository, ModeratorCategoryAssignmentRepository>();


            var jwtKey = builder.Configuration["Jwt:Key"];
            var jwtIssuer = builder.Configuration["Jwt:Issuer"];
            var jwtAudience = builder.Configuration["Jwt:Audience"];
            if (!string.IsNullOrEmpty(jwtKey))
            {
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                            ValidIssuer = jwtIssuer,
                            ValidAudience = jwtAudience,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                            ClockSkew = TimeSpan.Zero,
                            // Use the standard ASP.NET Core role claim type.
                            // JwtHelper also emits ClaimTypes.Role, so [Authorize(Roles=...)] works reliably.
                            RoleClaimType = ClaimTypes.Role
                        };
                    });
            }
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("UserOnly", policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireRole("USER", "AUTHOR", "ADMIN"));

                options.AddPolicy("AuthorOnly", policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireRole("AUTHOR", "ADMIN"));

                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireRole("ADMIN"));
            });
            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "AI Story Platform API",
                    Version = "v1"
                });
                // Cấu hình nút "Authorize"
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Nhập token JWT của bạn vào đây (không cần chữ Bearer)."
                });
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
                        new string[] {}
                    }
                });
                options.MapType<IFormFile>(() => new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary"
                });
            });

            var app = builder.Build();

            // =======================
            // HTTP pipeline
            // =======================

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Story Platform API v1");
                });
            }

            // In Development we often run on http://localhost:5000 (no HTTPS).
            // Enabling HTTPS redirection there breaks CORS preflight (OPTIONS) due to redirects.
            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            // Enable CORS
            app.UseCors("AllowClient");
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.Run();
        }
    }
}
