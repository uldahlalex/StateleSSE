# statele-sse

Zero-dependency EventSource client for [StateleSSE.AspNetCore](https://www.nuget.org/packages/StateleSSE.AspNetCore). Framework-agnostic — works with React, Vue, Svelte, Angular, or vanilla JS.

## Install

```
npm i statele-sse
```

## Quick start

```ts
import { StateleSSEClient } from 'statele-sse'

const sse = new StateleSSEClient('http://localhost:5000/sse')

const unsub = sse.listen(
    (id) => fetch(`/GetMessages?connectionId=${id}&roomId=abc`).then(r => r.json()),
    (messages) => console.log(messages)
)

// later:
unsub()
```

The constructor opens an EventSource and stores the `connectionId` from the server. `listen` calls your API with the connectionId, delivers initial data, then listens for SSE updates on the returned group. Returns a cleanup function.

## API

### `new StateleSSEClient(url, connectEvent?)`

| Param | Default | Description |
|---|---|---|
| `url` | — | Your SSE endpoint (e.g. `http://localhost:5000/sse`) |
| `connectEvent` | `'connected'` | SSE event name that delivers the connectionId |

Auto-connects immediately. Calls made to `listen` before the connection is ready are queued.

### `sse.listen<T>(register, onData, onError?)`

```ts
const unsub = sse.listen<Room[]>(
    (connectionId) => api.getRooms(connectionId),
    (rooms) => renderRooms(rooms),
    (error) => console.error(error)
)
```

`register` must return `Promise<{ group: string; data?: T }>`. This matches the `RealtimeListenResponse<T>` from StateleSSE.AspNetCore.

Returns a cleanup function that removes the listener.

### `sse.connectionId`

The connection ID assigned by the server, or `null` if not yet connected.

### `sse.status`

`'connecting'` | `'connected'` | `'disconnected'`

### `sse.onStatusChange`

```ts
sse.onStatusChange = (status) => console.log(status)
```

### `sse.disconnect()`

Closes the EventSource and clears all listeners.

## Reconnection

EventSource auto-reconnects natively. When the server assigns a new connectionId, all active `listen` subscriptions re-register automatically — server-side group membership and queries are restored without any consumer code.

## Framework examples

### React

```tsx
const SseContext = createContext<StateleSSEClient>(null!)

function SseProvider({ url, children }: { url: string; children: ReactNode }) {
    const [sse] = useState(() => new StateleSSEClient(url))
    useEffect(() => () => sse.disconnect(), [sse])
    return <SseContext.Provider value={sse}>{children}</SseContext.Provider>
}

function useRealtimeListen<T>(
    register: (id: string) => Promise<{ group: string; data?: T }>,
    onData: (data: T) => void,
    deps: unknown[] = []
) {
    const sse = useContext(SseContext)
    useEffect(() => sse.listen(register, onData), [sse.connectionId, ...deps])
}
```

Usage:

```tsx
// in main.tsx
<SseProvider url="http://localhost:5000/sse">
    <App />
</SseProvider>

// in a component
const [rooms, setRooms] = useState<Room[]>([])
useRealtimeListen(
    (id) => api.getRooms(id),
    (data) => setRooms(data)
)
```

### Vue

```ts
const sse = new StateleSSEClient('http://localhost:5000/sse')

export function useRealtimeListen<T>(
    register: (id: string) => Promise<{ group: string; data?: T }>,
    onData: (data: T) => void
) {
    onMounted(() => { unsub = sse.listen(register, onData) })
    onUnmounted(() => unsub?.())
}
```

### Vanilla JS

```ts
const sse = new StateleSSEClient('http://localhost:5000/sse')

const unsub = sse.listen(
    (id) => fetch(`/GetRooms?connectionId=${id}`).then(r => r.json()),
    (rooms) => document.getElementById('rooms').textContent = JSON.stringify(rooms)
)
```

## Server setup

I recommend seeing the full server docs on http://github.com/uldahlalex/statelesse but here's a brief walkthrough:

Install the NuGet package:

```
dotnet add package StateleSSE.AspNetCore
```

Minimal server (in-memory, single instance):

```csharp
// Program.cs
builder.Services.AddInMemorySseBackplane();
builder.Services.AddControllers();
```

With EF Core realtime queries:

```csharp
// Program.cs
builder.Services.AddInMemorySseBackplane();  // or AddRedisSseBackplane() for scaling
builder.Services.AddEfRealtime();
builder.Services.AddDbContext<MyDbContext>((sp, options) => {
    options.UseNpgsql(connectionString);
    options.AddEfRealtimeInterceptor(sp);
});
```

Controller:

```csharp
public class ChatController(ISseBackplane backplane, IRealtimeManager realtimeManager, MyDbContext ctx)
    : RealtimeControllerBase(backplane)
{
    [HttpGet(nameof(GetRooms))]
    public async Task<RealtimeListenResponse<List<Room>>> GetRooms(string connectionId)
    {
        var group = "rooms";
        await backplane.Groups.AddToGroupAsync(connectionId, group);

        realtimeManager.Subscribe<MyDbContext>(connectionId, group,
            criteria: changes => changes.OfType<Room>().Any(),
            query: async c => await c.Rooms.ToListAsync());

        return new RealtimeListenResponse<List<Room>>(group, ctx.Rooms.ToList());
    }
}
```

Any `SaveChanges` that touches a `Room` entity will re-execute the query and broadcast the result to all clients listening on the `"rooms"` group.

## License

MIT
