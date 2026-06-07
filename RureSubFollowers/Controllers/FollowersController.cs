using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RureSubFollowers.Model;
using RureSubFollowers.Models;
using RureSubFollowers.Services;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace RureSubFollowers.Controllers;

[ApiController]
[Route("/")]
public class FollowersController : Controller
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Follow(
        [FromServices] FollowersDbContext db,
        [FromServices] IConnectionMultiplexer redis,
        [FromServices] ISnowflakeIdGenerator snowflakeIdGenerator,
        [FromQuery] Guid? followingId)
    {
        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Unauthorized();
        }

        if (followingId == null || userId == followingId)
        {
            return BadRequest();
        }

        if (db.Subscriptions.Any(s => s.FollowerId == userId && s.FollowingId == followingId))
        {
            return BadRequest();
        }

        var subscription = new Subscription
        {
            FollowerId = userId,
            FollowingId = followingId.Value,
            FollowedAt = DateTime.UtcNow
        };

        var outboxMessage = new OutboxMessage
        {
            OccuredAt = DateTime.UtcNow,
            Topic = "user-followed",
            Content = JsonSerializer.Serialize(subscription)
        };

        db.Subscriptions.Add(subscription);
        db.OutboxMessages.Add(outboxMessage);

        #region Redis

        var redisDb = redis.GetDatabase();

        var followerRedisIdKey = $"user:id:{userIdRaw.Value}";
        var followingRedisIdKey = $"user:id:{followingId.Value}";

        var followerRedisId = await redisDb.StringGetAsync(followerRedisIdKey);
        var followingRedisId = await redisDb.StringGetAsync(followingRedisIdKey);

        if (followerRedisId.IsNull)
        {
            var newId = snowflakeIdGenerator.NextId();
            await redisDb.StringSetAsync(followerRedisIdKey, newId);
            followerRedisIdKey = newId.ToString();
        }
        if (followingRedisId.IsNull)
        {
            var newId = snowflakeIdGenerator.NextId();
            await redisDb.StringSetAsync(followingRedisIdKey, newId);
            followingRedisId = newId.ToString();
        }

        var followedAtOffset = new DateTimeOffset(subscription.FollowedAt).ToUnixTimeMilliseconds();
        var isFollowerAdded = await redisDb.SortedSetAddAsync($"user:{followerRedisId}:following", followingRedisId, followedAtOffset); 
        var isFollowingAdded = await redisDb.SortedSetAddAsync($"user:{followingRedisId}:followers", followerRedisId, followedAtOffset);

        #endregion

        await db.SaveChangesAsync();

        return Ok("Hello world!");
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Unfollow(
        [FromServices] FollowersDbContext db,
        [FromServices] IConnectionMultiplexer redis,
        [FromQuery] Guid? followingId)
    {
        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            return Unauthorized();
        }

        if (followingId == null || userId == followingId)
        {
            return BadRequest();
        }

        var sub = db.Subscriptions.FirstOrDefault(s => s.FollowerId == userId && s.FollowingId == followingId);

        if (sub == null)
        {
            return NotFound();
        }

        var outboxMessage = new OutboxMessage
        {
            OccuredAt = DateTime.UtcNow,
            Topic = "user-unfollowed",
            Content = JsonSerializer.Serialize(sub)
        };

        db.Subscriptions.Remove(sub);
        db.OutboxMessages.Add(outboxMessage);

        #region Redis

        var redisDb = redis.GetDatabase();

        var followerRedisIdKey = $"user:id:{userIdRaw.Value}";
        var followingRedisIdKey = $"user:id:{followingId.Value}";

        var followerRedisId = await redisDb.StringGetAsync(followerRedisIdKey);
        var followingRedisId = await redisDb.StringGetAsync(followingRedisIdKey);

        if (!followerRedisId.IsNull && !followingRedisId.IsNull)
        {
            var isFollowerAdded = await redisDb.SortedSetRemoveAsync($"user:{followerRedisId}:following", followingRedisId);
            var isFollowingAdded = await redisDb.SortedSetRemoveAsync($"user:{followingRedisId}:followers", followerRedisId);
        }

        #endregion

        await db.SaveChangesAsync();

        return Ok("Hello world!");
    }
}
