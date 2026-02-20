using Microsoft.EntityFrameworkCore;

namespace server;

[PrimaryKey(nameof(Id))]
public class Poke
{
    public string Id { get; set; }
    public string FromUserId { get; set; }
    public string ToUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
