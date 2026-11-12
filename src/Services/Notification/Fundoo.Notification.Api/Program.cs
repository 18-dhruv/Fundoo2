using Fundoo.Notification.Api.Services;
using Fundoo.Notification.Application.Abstractions;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Register controllers
builder.Services.AddControllers();

// Register Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<SmtpBulkhead>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Fundoo Notification API",
            Version = "v1",
            Description = "API for sending Fundoo email notifications"
        });
});

// Register Notification Service
builder.Services.AddTransient<INotificationService, SmtpEmailService>();

var app = builder.Build();

// Configure Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Map controller routes
app.MapControllers();

app.Run();