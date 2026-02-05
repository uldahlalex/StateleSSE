import {
    createContext,
    useContext,
    useEffect,
    useRef,
    useState,
    type ReactNode,
} from "react";

// ============================================================================
// Base message contract
// ============================================================================

/**
 * Minimal contract for messages received from the server.
 * Messages must have an eventType field for routing.
 */
interface BaseResponseDto {
    eventType?: string;
}

// ============================================================================
// Types
// ============================================================================

export interface StreamConfig {
    /** The SSE endpoint URL (e.g., "http://localhost:5000/Connect") */
    urlForStreamEndpoint: string;
    /** The SSE event name that delivers the connection response (e.g., "ConnectionResponse") */
    connectEvent: string;
}

type Unsubscribe = () => void;

export interface Stream {
    /** The connection ID assigned by the server */
    connectionId: string | null;
    /** Whether the connection is established */
    isConnected: boolean;
    /**
     * Listen for messages on a group and react to a specific eventType.
     * Returns a cleanup function to stop listening.
     *
     * Note: Group membership is managed server-side. This method only
     * determines how to handle messages that arrive on a given group.
     *
     * @param group - The group name (messages arrive here when the server sends to this group)
     * @param eventType - The eventType to react to
     * @param handler - Callback invoked when a matching message arrives
     *
     * @example
     * stream.on<MessageResponseDto>('chatRoom', 'MessageResponseDto', (dto) => {
     *     console.log(dto.message);
     * });
     */
    on<T>(
        group: string,
        eventType: string,
        handler: (dto: T) => void
    ): Unsubscribe;
    /** Listen for all messages on a group (no eventType filtering). Used by EfRealtime subscriptions. */
    on<T>(
        group: string,
        handler: (dto: T) => void
    ): Unsubscribe;
}

// ============================================================================
// Errors
// ============================================================================

export class StreamError extends Error {
    constructor(message: string) {
        super(`[useStream] ${message}`);
        this.name = "StreamError";
    }
}

function assertNonEmpty(value: string, name: string): void {
    if (!value || value.trim() === "") {
        throw new StreamError(`${name} cannot be empty`);
    }
}

// ============================================================================
// Internal listener registry
// ============================================================================

type Listener = {
    group: string;
    eventType: string | null;
    handler: (dto: unknown) => void;
};

class StreamCore {
    private eventSource: EventSource | null = null;
    private listeners = new Map<symbol, Listener>();
    private groupHandlers = new Map<string, (e: MessageEvent) => void>();
    private pendingGroups = new Set<string>();
    private isDisconnected = false;

    connectionId: string | null = null;
    onConnectionChange: ((id: string | null) => void) | null = null;

    connect(config: StreamConfig) {
        assertNonEmpty(config.urlForStreamEndpoint, "config.url");
        assertNonEmpty(config.connectEvent, "config.connectEvent");

        if (this.eventSource) {
            throw new StreamError("Already connected. Call disconnect() first.");
        }

        this.isDisconnected = false;
        this.eventSource = new EventSource(config.urlForStreamEndpoint);

        // Attach handlers for any groups registered before connect
        for (const group of this.pendingGroups) {
            this.attachGroupHandler(group);
        }
        this.pendingGroups.clear();

        this.eventSource.addEventListener(config.connectEvent, (e) => {
            const data = JSON.parse(e.data);
            this.connectionId = data?.connectionId ?? null;
            this.onConnectionChange?.(this.connectionId);
        });

        this.eventSource.onerror = () => {
            console.error("[stream] EventSource connection error");
        };
    }

    disconnect() {
        this.isDisconnected = true;
        this.eventSource?.close();
        this.eventSource = null;
        this.connectionId = null;
        this.listeners.clear();
        this.groupHandlers.clear();
        this.pendingGroups.clear();
    }

    on<T>(group: string, eventType: string, handler: (dto: T) => void): Unsubscribe;
    on<T>(group: string, handler: (dto: T) => void): Unsubscribe;
    on<T>(
        group: string,
        eventTypeOrHandler: string | ((dto: T) => void),
        maybeHandler?: (dto: T) => void
    ): Unsubscribe {
        assertNonEmpty(group, "group");

        const eventType = typeof eventTypeOrHandler === "string" ? eventTypeOrHandler : null;
        const handler = typeof eventTypeOrHandler === "function" ? eventTypeOrHandler : maybeHandler!;

        if (eventType !== null) assertNonEmpty(eventType, "eventType");
        if (typeof handler !== "function") throw new StreamError("handler must be a function");

        if (this.isDisconnected) {
            throw new StreamError(
                "Cannot register listener after disconnect. This usually means you're calling on() outside of a useEffect, or the component unmounted."
            );
        }

        const key = Symbol();

        this.listeners.set(key, {
            group,
            eventType,
            handler: handler as (dto: unknown) => void,
        });
        this.ensureGroupHandler(group);

        return () => {
            this.listeners.delete(key);
            this.maybeRemoveGroupHandler(group);
        };
    }

    private ensureGroupHandler(group: string) {
        if (this.groupHandlers.has(group)) return;

        if (!this.eventSource) {
            // EventSource not ready yet, queue for later
            this.pendingGroups.add(group);
            return;
        }

        this.attachGroupHandler(group);
    }

    private attachGroupHandler(group: string) {
        if (this.groupHandlers.has(group) || !this.eventSource) return;

        const handler = (e: MessageEvent) => {
            let data: unknown;
            try {
                data = JSON.parse(e.data);
            } catch {
                console.error(`[stream] Failed to parse message on group "${group}":`, e.data);
                return;
            }

            const eventType = (data as BaseResponseDto)?.eventType;

            for (const listener of this.listeners.values()) {
                if (listener.group !== group) continue;
                if (listener.eventType && listener.eventType !== eventType) continue;
                try {
                    listener.handler(data);
                } catch (err) {
                    console.error(`[stream] Handler error for ${group}:`, err);
                }
            }
        };

        this.eventSource.addEventListener(group, handler);
        this.groupHandlers.set(group, handler);
    }

    private maybeRemoveGroupHandler(group: string) {
        // Check if any listener still needs this group
        for (const listener of this.listeners.values()) {
            if (listener.group === group) return;
        }

        const handler = this.groupHandlers.get(group);
        if (handler && this.eventSource) {
            this.eventSource.removeEventListener(group, handler);
        }
        this.groupHandlers.delete(group);
    }
}

// ============================================================================
// Context
// ============================================================================

const StreamContext = createContext<Stream | null>(null);

// ============================================================================
// Provider
// ============================================================================

export interface StreamProviderProps {
    config: StreamConfig;
    children: ReactNode;
}

export function StreamProvider({ config, children }: StreamProviderProps) {
    const coreRef = useRef<StreamCore | null>(null);
    const [connectionId, setConnectionId] = useState<string | null>(null);

    if (!coreRef.current) {
        coreRef.current = new StreamCore();
    }

    useEffect(() => {
        const core = coreRef.current!;
        core.onConnectionChange = setConnectionId;
        core.connect(config);

        return () => {
            core.disconnect();
        };
    }, [config.urlForStreamEndpoint, config.connectEvent]);

    const stream: Stream = {
        connectionId,
        isConnected: connectionId !== null,
        on: (group: string, ...args: [string, (dto: unknown) => void] | [(dto: unknown) => void]) =>
            (coreRef.current!.on as Function)(group, ...args),
    };

    return (
        <StreamContext.Provider value={stream}>
            {children}
        </StreamContext.Provider>
    );
}

// ============================================================================
// Hook
// ============================================================================

/**
 * Access the stream for listening to messages from groups.
 * Must be used within a StreamProvider like this:
 * @example
 * import {createRoot} from 'react-dom/client'
 * import './index.css'
 * import App from './App.tsx'
 * import {StreamProvider} from "./useStream.tsx";
 *
 * createRoot(document.getElementById('root')!).render(
 *     <StreamProvider config={{
 *         connectEvent: 'connected',
 *         urlForStreamEndpoint: 'http://localhost:5000/connect'
 *     }}>
 *         <App/>
 *     </StreamProvider>,
 * )
 *
 * Group membership is managed server-side via the StateleSSE backplane.
 * This hook only determines how to react when messages arrive.
 *
 * @example
 * const stream = useStream();
 *
 * useEffect(() => {
 *     // Listen for messages on 'chatRoom' group, react to 'MessageResponseDto' events
 *     const cleanup = stream.on<MessageResponseDto>('chatRoom', 'MessageResponseDto', (dto) => {
 *         console.log(dto.message);
 *     });
 *     return cleanup;
 * }, []);
 *
 * @throws {StreamError} If used outside of StreamProvider
 */
export function useStream(): Stream {
    const stream = useContext(StreamContext);
    if (!stream) {
        throw new StreamError(
            "useStream must be used within a StreamProvider. " +
            "Wrap your app with <StreamProvider config={...}>."
        );
    }
    return stream;
}

// ============================================================================
// EfRealtime hook
// ============================================================================

/**
 * Subscribe to a realtime feature and listen for broadcasts.
 * Combines the API call (which registers the server-side subscription) with
 * the SSE listener (which receives broadcasts when SaveChanges triggers).
 *
 * @param subscribe - Async function that calls the subscribe endpoint. Receives the connectionId
 *                    and must return an object with a `group` field (RealtimeListenResponse).
 * @param onData - Callback invoked whenever the server broadcasts new data for this subscription.
 * @param deps - Additional dependency array for re-subscribing (e.g., [roomId]).
 *
 * @example
 * useRealtimeListen<Message[]>(
 *     (connId) => chatClient.listenToRoomMessages(roomId, connId),
 *     (messages) => setMessages(messages),
 *     [roomId]
 * );
 */
export function useRealtimeListen<T>(
    subscribe: (connectionId: string) => Promise<{ group: string }>,
    onData: (data: T) => void,
    deps: unknown[] = []
) {
    const stream = useStream();

    useEffect(() => {
        if (!stream.connectionId) return;

        let unsub: Unsubscribe | undefined;
        let cancelled = false;

        subscribe(stream.connectionId).then(({ group }) => {
            if (cancelled) return;
            unsub = stream.on<T>(group, onData);
        });

        return () => {
            cancelled = true;
            unsub?.();
        };
    }, [stream.connectionId, ...deps]);
}
