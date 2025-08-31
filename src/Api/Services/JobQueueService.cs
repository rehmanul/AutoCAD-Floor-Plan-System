using FloorPlanAPI.Models;
using StackExchange.Redis;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace FloorPlanAPI.Services
{
    public interface IJobQueueService
    {
        Task EnqueueJobAsync(string jobId, ProcessingSettings settings, string inputFileUrl);
        Task<QueuedJob?> DequeueJobAsync();
    }

    public class RedisJobQueueService : IJobQueueService
    {
        private readonly IDatabase _redis;
        private const string QueueKey = "floorplan:jobs";

        public RedisJobQueueService(IConnectionMultiplexer redis)
        {
            _redis = redis.GetDatabase();
        }

        public async Task EnqueueJobAsync(string jobId, ProcessingSettings settings, string inputFileUrl)
        {
            var queuedJob = new QueuedJob
            {
                JobId = jobId,
                Settings = settings,
                InputFileUrl = inputFileUrl,
                QueuedAt = System.DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(queuedJob);
            await _redis.ListLeftPushAsync(QueueKey, json);
        }

        public async Task<QueuedJob?> DequeueJobAsync()
        {
            var json = await _redis.ListRightPopAsync(QueueKey);
            if (!json.HasValue) return null;
            return JsonSerializer.Deserialize<QueuedJob>(json!);
        }
    }

    public class InMemoryJobQueueService : IJobQueueService
    {
        private readonly ConcurrentQueue<QueuedJob> _queue = new();

        public Task EnqueueJobAsync(string jobId, ProcessingSettings settings, string inputFileUrl)
        {
            var queuedJob = new QueuedJob
            {
                JobId = jobId,
                Settings = settings,
                InputFileUrl = inputFileUrl,
                QueuedAt = DateTime.UtcNow
            };
            _queue.Enqueue(queuedJob);
            return Task.CompletedTask;
        }

        public Task<QueuedJob?> DequeueJobAsync()
        {
            _queue.TryDequeue(out var job);
            return Task.FromResult(job);
        }
    }
}