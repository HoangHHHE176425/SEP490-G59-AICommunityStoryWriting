using Microsoft.OpenApi.Models;
using Repositories;
using Services.Implementations;
using Services.Interfaces;

namespace AIStory.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // =======================
            // Add services
            // =======================

            builder.Services.AddControllers();

            // CORS Configuration
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowClient", policy =>
                {
                    policy.WithOrigins(
                            "http://localhost:5210",
                            "http://localhost:5000",
                            "http://localhost:3000",
                            "http://localhost:8080",
                            "https://localhost:7258",
                            "http://localhost:16164"
                        )
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            // Dependency Injection
            builder.Services.AddScoped<IStoryRepository, StoryRepository>();
            builder.Services.AddScoped<IStoryService, StoryService>();
            builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
            builder.Services.AddScoped<ICategoryService, CategoryService>();
            builder.Services.AddScoped<IChapterRepository, ChapterRepository>();
            builder.Services.AddScoped<IChapterService, ChapterService>();

            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "AI Story Platform API",
                    Version = "v1"
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

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            // Enable CORS
            app.UseCors("AllowClient");

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
