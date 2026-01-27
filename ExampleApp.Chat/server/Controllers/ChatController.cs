using StateleSSE.AspNetCore;

public class ChatController
{
    public async Task Connect()
    {
        await using var sse = await HttpContext.OpenSseStreamAsync();
    }   
}