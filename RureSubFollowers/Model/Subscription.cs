namespace RureSubFollowers.Model;

public class Subscription
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FollowerId { get; set; }
    public Guid FollowingId { get; set; }

    public DateTime FollowedAt { get; set; }
}
