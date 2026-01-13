using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace server;

public class SseEndpointConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            foreach (var action in controller.Actions)
            {
                if (action.ActionName.Contains("Stream", StringComparison.OrdinalIgnoreCase))
                {
                    var hasProducesAttribute = action.Filters
                        .OfType<ProducesAttribute>()
                        .Any(p => p.ContentTypes.Contains("text/event-stream"));

                    if (!hasProducesAttribute)
                    {
                        action.Filters.Add(new ProducesAttribute("text/event-stream"));
                    }
                }
            }
        }
    }
}
