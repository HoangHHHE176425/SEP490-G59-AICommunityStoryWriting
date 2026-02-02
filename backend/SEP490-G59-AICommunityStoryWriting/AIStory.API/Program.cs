using Microsoft.OpenApi.Models;
using Repositories;
using Services.Implementations;

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

            // Dependency Injection
            builder.Services.AddScoped<IStoryRepository, StoryRepository>();
            builder.Services.AddScoped<IStoryService, StoryService>();

            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "AI Story Platform API",
                    Version = "v1",
                    Description = "RESTful API for Story Platform"
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

            // Auth (JWT s? g?n sau)
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
