namespace KestrelHub.Shared.Models;

public class AgentHeartbeat
{
    public Guid AgentId { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsHealthy { get; set; }
}
