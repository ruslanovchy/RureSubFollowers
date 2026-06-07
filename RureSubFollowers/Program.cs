using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RureSubFollowers.Model;
using RureSubFollowers.Services;
using RureSubIdentity.Services;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
    
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ISnowflakeIdGenerator>(sp =>
{
    return new SnowflakeIdGenerator(1);
});

#region Kafka

var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
var kafkaGroupId = builder.Configuration["Kafka:GroupId"];

if (string.IsNullOrEmpty(kafkaBootstrapServers) || string.IsNullOrEmpty(kafkaGroupId))
{
    throw new Exception("Kafka was not configured!");
}

var kafkaConsumerConfig = new ConsumerConfig
{
    GroupId = kafkaGroupId,
    BootstrapServers = kafkaBootstrapServers,
    AutoOffsetReset = AutoOffsetReset.Earliest,
    EnableAutoCommit = false,
    EnableAutoOffsetStore = false,
};

var kafkaProducerConfig = new ProducerConfig
{
    BootstrapServers = kafkaBootstrapServers
};

builder.Services.AddSingleton(kafkaConsumerConfig);
builder.Services.AddSingleton(kafkaProducerConfig);

#endregion

#region Redis

var redisConnectionString = builder.Configuration["Redis:ConnectionString"];

if (string.IsNullOrEmpty(redisConnectionString))
{
    throw new Exception("Redis was not configured!");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

#endregion

#region JWT

var jwtKeyString = builder.Configuration["JWT:Key"];

if (string.IsNullOrEmpty(jwtKeyString))
{
    throw new Exception("JWT was not configured!");
}

var jwtKey = Encoding.UTF8.GetBytes(jwtKeyString);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,

        IssuerSigningKey = new SymmetricSecurityKey(jwtKey)
    };
});

#endregion

#region

var dbConnectionString = builder.Configuration.GetConnectionString("Db");

if (string.IsNullOrEmpty(dbConnectionString))
{
    throw new Exception("Db connection string was not configured!");
}

builder.Services.AddDbContext<FollowersDbContext>(options =>
{
    options.UseNpgsql(dbConnectionString);
});

#endregion

builder.Services.AddHostedService<OutboxWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseHttpsRedirection();
}


app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Followers}/{action=Index}/{id?}");

app.Run();
