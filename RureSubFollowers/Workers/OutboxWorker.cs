using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using RureSubFollowers.Model;
using RureSubFollowers.Models;

namespace RureSubIdentity.Services;

public class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IProducer<string, string> producer;

    public OutboxWorker(IServiceScopeFactory scopeFactory, ProducerConfig config)
    {
        this.scopeFactory = scopeFactory;
        producer = new ProducerBuilder<string, string>(config).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<FollowersDbContext>();

            var messages = await db.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.OccuredAt)
                .Take(20)
                .ToListAsync(stoppingToken);

            foreach (var message in messages)
            {
                try
                {
                    await producer.ProduceAsync(message.Topic, new Message<string, string> { Key = message.Id.ToString(), Value = message.Content }, stoppingToken);
                    message.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    message.Error = ex.Message;
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(1000, stoppingToken);
        }
    }
}
