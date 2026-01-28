namespace StateleSSE.AspNetCore;

/// <summary>
/// Models used for server sent events to clients should preferably use this model.
/// It automatically attaches "EventType" as a property with type name as value.
/// </summary>
public abstract record BaseResponseDto
{
    public BaseResponseDto()
    {
        EventType = GetType().Name;
       
    }

    public string EventType { get; set; }
}