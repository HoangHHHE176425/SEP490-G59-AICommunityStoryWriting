using AIStory.API.BackgroundServices;
using AIStory.API.Configurations;
using AIStory.API.Hubs;
using AIStory.API.Services;
using AIStory.Services.Helpers;
using AIStory.Services.Implementations;
using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repositories;
using Repositories.Implementations;
using Repositories.Interfaces;
using Services.Implementations;
using Services.Implementations.Lookups;
using Services.Integrations.PayOS;
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

            builder.Services.AddMemoryCache();
            builder.Services.Configure<CloudinarySettings>(
                builder.Configuration.GetSection(CloudinarySettings.SectionName));
            builder.Services.AddSingleton<ICloudinaryImageService, CloudinaryImageService>();
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.WriteIndented = true;
                });
            // Đăng ký DbContext, để OnConfiguring trong StoryPlatformDbContext tự cấu hình connection string.
            builder.Services.AddDbContext<StoryPlatformDbContext>();

            var corsExtraOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? Array.Empty<string>();
            var corsExtraSet = new HashSet<string>(corsExtraOrigins, StringComparer.OrdinalIgnoreCase);

            // CORS Configuration
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowClient", policy =>
                {

                    policy.SetIsOriginAllowed(origin =>
                    {
                        if (string.IsNullOrWhiteSpace(origin)) return false;
                        if (corsExtraSet.Contains(origin)) return true;
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
            builder.Services.AddScoped<ITokenService, TokenService>();
            // Dependency Injection
            // dj for auth va user
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IOtpRepository, OtpRepository>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IEmailService, EmailService>();

            builder.Services.AddScoped<IStoryRepository, StoryRepository>();
            builder.Services.AddScoped<IStoryService, StoryService>();
            builder.Services.AddScoped<IUserLookup, UserLookup>();
            builder.Services.AddScoped<ICategoryLookup, CategoryLookup>();
            builder.Services.AddScoped<IStoryLookup, StoryLookup>();
            builder.Services.AddScoped<IUserActivityLookup, UserActivityLookup>();
            builder.Services.AddScoped<IStoryCommentCommand, StoryCommentCommand>();
            builder.Services.AddScoped<ICommentReactionReader, CommentReactionReader>();
            builder.Services.AddScoped<IStoryCommentPostService, StoryCommentPostService>();
            builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
            builder.Services.AddScoped<ICategoryService, CategoryService>();
            builder.Services.AddScoped<IChapterRepository, ChapterRepository>();
            builder.Services.AddScoped<IChapterService, ChapterService>();
            builder.Services.AddScoped<IChapterVersionRepository, ChapterVersionRepository>();
            builder.Services.AddScoped<IChapterVersionService, ChapterVersionService>();

            // Policies
            builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
            builder.Services.AddScoped<IAuthorPolicyAcceptanceRepository, AuthorPolicyAcceptanceRepository>();
            builder.Services.AddScoped<IPolicyService, PolicyService>();
            builder.Services.AddScoped<IAdminPolicyService, AdminPolicyService>();
            builder.Services.AddScoped<IAdminUserService, AdminUserService>();
            builder.Services.AddScoped<IModeratorCategoryAssignmentRepository, ModeratorCategoryAssignmentRepository>();
            builder.Services.AddScoped<IReviewDeadlineForfeitureService, ReviewDeadlineForfeitureService>();
            builder.Services.AddScoped<IModerationService, ModerationService>();
            builder.Services.AddHostedService<ReviewDeadlineForfeitureBackgroundService>();
            builder.Services.AddScoped<IReviewEscalationService, ReviewEscalationService>();
            builder.Services.AddScoped<IAdminUnifiedEscalationService, AdminUnifiedEscalationService>();
            builder.Services.AddScoped<IStoryReportService, StoryReportService>();
            builder.Services.AddScoped<ICommentReportService, CommentReportService>();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<IUserIdProvider, SignalRUserIdProvider>();
            builder.Services.AddScoped<IModerationHubNotifier, ModerationHubNotifier>();
            builder.Services.AddScoped<INotificationHubNotifier, NotificationHubNotifier>();

            // AI: Story Memory Engine (RAG khi đã index) + các agent gợi ý/đồng sáng tác
            builder.Services.AddScoped<IStoryContextBuilder, StoryContextBuilder>();
            builder.Services.AddScoped<IContentGuardrailService, ContentGuardrailService>();
            builder.Services.AddScoped<IAIUsageLogRepository, AIUsageLogRepository>();
            builder.Services.AddScoped<IStoryCharacterMemoryRepository, StoryCharacterMemoryRepository>();
            builder.Services.AddScoped<IStoryEventMemoryRepository, StoryEventMemoryRepository>();
            builder.Services.AddScoped<IStoryStoryStateRepository, StoryStoryStateRepository>();
            builder.Services.AddSingleton<IVectorStore, FaissVectorStore>();
            builder.Services.AddScoped<IStoryRagService, StoryRagService>();
            builder.Services.AddScoped<IStoryMemoryEngine, StoryMemoryEngine>();
            builder.Services.AddScoped<IChapterMemoryAnalysisService, ChapterMemoryAnalysisService>();
            builder.Services.AddScoped<IAINextChapterService, AINextChapterService>();
            builder.Services.AddScoped<IAICoCreationService, AICoCreationService>();
            builder.Services.AddScoped<IChapterCheckService, ChapterCheckService>();
            builder.Services.AddScoped<IAiGeneratedContentRepository, AiGeneratedContentRepository>();
            builder.Services.AddScoped<IAiSensitiveWordsRepository, AiSensitiveWordsRepository>();
            builder.Services.AddScoped<IAiConfigsRepository, AiConfigsRepository>();
            builder.Services.AddScoped<IAIUsageLimitConfigService, AIUsageLimitConfigService>();
            builder.Services.AddScoped<IChapterCompareService, ChapterCompareService>();
            builder.Services.AddScoped<IChapterVersionAiCompareService, ChapterVersionAiCompareService>();
            builder.Services.AddSingleton<IAISuggestRateLimitService, AISuggestRateLimitService>();

            // Coin / PayOS
            builder.Services.AddHttpClient<PayOSClient>();
            builder.Services.AddScoped<IPayOSClient, PayOSClientAdapter>();
            builder.Services.AddScoped<ICoinPaymentService, CoinPaymentService>();
            builder.Services.AddHostedService<PayOSPendingOrderSyncService>();

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
                        // SignalR: accept JWT from query string (WebSocket cannot send custom headers)
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                var accessToken = context.Request.Query["access_token"];
                                var path = context.HttpContext.Request.Path;
                                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                                {
                                    context.Token = accessToken;
                                }
                                return Task.CompletedTask;
                            }
                        };
                    });
            }
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("UserOnly", policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireRole("USER", "AUTHOR", "ADMIN", "MODERATOR", "COMPLIANCE"));

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

            // Nginx (or another reverse proxy) terminates TLS and forwards http://127.0.0.1:5000
            var forwardedHeadersOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            forwardedHeadersOptions.KnownNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();
            forwardedHeadersOptions.KnownProxies.Add(System.Net.IPAddress.Loopback);
            forwardedHeadersOptions.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
            app.UseForwardedHeaders(forwardedHeadersOptions);

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Biến môi trường đọc trực tiếp (ưu tiên hơn appsettings*.Local.json đè systemd).
            static bool EnvIsTrue(string name) =>
                string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase);

            var swaggerFromEnv = EnvIsTrue("Swagger__Enabled") || EnvIsTrue("Swagger__EnableInProduction");
            var swaggerEnabled = app.Environment.IsDevelopment()
                || swaggerFromEnv
                || app.Configuration.GetValue("Swagger:Enabled", false)
                || app.Configuration.GetValue("Swagger:EnableInProduction", false);
            if (swaggerEnabled)
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Story Platform API v1");
                });
            }

            // In Development we often run on http://localhost:5000 (no HTTPS).
            // Enabling HTTPS redirection there breaks CORS preflight (OPTIONS) due to redirects.
            // Behind nginx with TLS, forwarded X-Forwarded-Proto keeps scheme correct so redirects behave.
            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            // Trang FE trên Internet (vd. http://103.x) gọi API trên localhost/LAN — Chrome yêu cầu
            // Private Network Access: preflight OPTIONS phải có Access-Control-Allow-Private-Network.
            app.Use(async (context, next) =>
            {
                if (HttpMethods.IsOptions(context.Request.Method) &&
                    context.Request.Headers.TryGetValue("Access-Control-Request-Private-Network", out var pna) &&
                    string.Equals(pna.ToString(), "true", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers.Append("Access-Control-Allow-Private-Network", "true");
                        return Task.CompletedTask;
                    });
                }
                await next();
            });

            // Enable CORS
            app.UseCors("AllowClient");
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<ModeratorHub>("/hubs/moderator");
            app.MapHub<NotificationHub>("/hubs/notifications");
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<StoryPlatformDbContext>();

                string hashedPassword = BCrypt.Net.BCrypt.HashPassword("123456");

                users CreateUser(string email, string role, int ageDays)
                {
                    var createdAt = DateTime.Now.AddDays(-ageDays);

                    var user = new users
                    {
                        id = Guid.NewGuid(),
                        email = email,
                        password_hash = hashedPassword,
                        role = role,
                        status = "ACTIVE",
                        created_at = createdAt,
                        updated_at = createdAt
                    };

                    return user;
                }

                void AddUserWithProfile(string email, string role, string nickname, string bio, int ageDays)
                {
                    if (!context.users.Any(u => u.email == email))
                    {
                        var user = CreateUser(email, role, ageDays);

                        context.users.Add(user);

                        context.user_profiles.Add(new user_profiles
                        {
                            user_id = user.id, // FK chuẩn theo DB
                            nickname = nickname,
                            bio = bio,
                            updated_at = DateTime.Now   
                        });
                    }
                }

                // ================= ADMIN (1 account duy nhất) =================
                AddUserWithProfile(
                    "admin@aistory.com",
                    "ADMIN",
                    "Nguyễn Minh Quân",
                    "System Administrator",
                    500
                );

                // ================= AUTHOR =================
                AddUserWithProfile("hoang.nguyen@aistory.com", "AUTHOR", "Hoàng Nguyễn", "Tác giả fantasy", 300);
                AddUserWithProfile("linh.tran@aistory.com", "AUTHOR", "Linh Trần", "Tác giả ngôn tình", 280);
                AddUserWithProfile("tuan.pham@aistory.com", "AUTHOR", "Tuấn Phạm", "Tác giả hành động", 260);

                // ================= MODERATOR =================
                AddUserWithProfile("hieu.le@aistory.com", "MODERATOR", "Hiếu Lê", "Moderator", 250);
                AddUserWithProfile("anh.do@aistory.com", "MODERATOR", "Anh Đỗ", "Moderator", 240);

                // ================= COMPLIANCE =================
                AddUserWithProfile("thao.vo@aistory.com", "COMPLIANCE", "Thảo Võ", "Compliance", 220);
                AddUserWithProfile("khanh.bui@aistory.com", "COMPLIANCE", "Khánh Bùi", "Compliance", 210);

                // ================= USER =================
                AddUserWithProfile("nam.nguyen@aistory.com", "USER", "Nam Nguyễn", "Độc giả mới", 10);
                AddUserWithProfile("hoa.pham@aistory.com", "USER", "Hoa Phạm", "Đọc truyện mỗi ngày", 60);
                AddUserWithProfile("long.tran@aistory.com", "USER", "Long Trần", "Fan truyện lâu năm", 200);

                context.SaveChanges();
            }
            app.Run();
        }
    }
}
