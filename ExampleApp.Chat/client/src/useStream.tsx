import {
    createContext,
    useContext,
    useEffect,
    useRef,
    useState,
    type ReactNode,
} from "react";
import type {
    BaseResponseDto,
    ConnectionResponse,
    JoinGroupResponse,
    MessageResponseDto,
} from "./generated-ts-client";
const isProd = import.meta.env.PROD;

// ============================================================================
// Event Type Map - Add your event types here for compile-time safety
// ============================================================================

/**
 * Maps eventType strings to their DTO types.
 * Add new event types here to get compile-time type checking.
 */
export interface EventTypeMap {
    ConnectionResponse: ConnectionResponse;
    JoinGroupResponse: JoinGroupResponse;
    MessageResponseDto: MessageResponseDto;
    UserLeftResponseDto: { connectionId: string; eventType: string };
}

/** Valid event type names */
export type EventTypeName = keyof EventTypeMap;

// ============================================================================
// Types
// ============================================================================

export interface StreamConfig {
    /** The SSE endpoint URL (e.g., "http://localhost:5000/Connect") */
    url: string;
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
     * Subscribe to events on a channel (group) filtered by eventType.
     * Returns an unsubscribe function.
     *
     * @param channel - The SSE event name / group ID
     * @param eventType - The eventType property on the DTO (must be a key of EventTypeMap)
     * @param handler - Callback receiving the typed DTO
     */
    on<K extends EventTypeName>(
        channel: string,
        eventType: K,
        handler: (dto: EventTypeMap[K]) => void
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

function assertDefined<T>(value: T | null | undefined, name: string): asserts value is T {
    if (value === null || value === undefined) {
        throw new StreamError(`${name} is not defined. Make sure StreamProvider is mounted.`);
    }
}

// ============================================================================
// Internal subscription registry
// ============================================================================

type Subscription = {
    channel: string;
    eventType: string;
    handler: (dto: unknown) => void;
};

class StreamCore {
    private eventSource: EventSource | null = null;
    private subscriptions = new Map<symbol, Subscription>();
    private channelListeners = new Map<string, (e: MessageEvent) => void>();
    private pendingChannels = new Set<string>();
    private isDisconnected = false;

    connectionId: string | null = null;
    onConnectionChange: ((id: string | null) => void) | null = null;

    connect(config: StreamConfig) {
        assertNonEmpty(config.url, "config.url");
        assertNonEmpty(config.connectEvent, "config.connectEvent");

        if (this.eventSource) {
            throw new StreamError("Already connected. Call disconnect() first.");
        }

        this.isDisconnected = false;
        this.eventSource = new EventSource(config.url);

        // Attach listeners for any subscriptions registered before connect
        for (const channel of this.pendingChannels) {
            this.attachChannelListener(channel);
        }
        this.pendingChannels.clear();

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
        this.subscriptions.clear();
        this.channelListeners.clear();
        this.pendingChannels.clear();
    }

    on<K extends EventTypeName>(
        channel: string,
        eventType: K,
        handler: (dto: EventTypeMap[K]) => void
    ): Unsubscribe {
        // Validation
        assertNonEmpty(channel, "channel");
        assertNonEmpty(eventType, "eventType");

        if (typeof handler !== "function") {
            throw new StreamError("handler must be a function");
        }

        if (this.isDisconnected) {
            throw new StreamError(
                "Cannot subscribe after disconnect. This usually means you're calling on() outside of a useEffect, or the component unmounted."
            );
        }

        const key = Symbol();

        this.subscriptions.set(key, {
            channel,
            eventType,
            handler: handler as (dto: unknown) => void,
        });
        this.ensureChannelListener(channel);

        return () => {
            this.subscriptions.delete(key);
            this.maybeRemoveChannelListener(channel);
        };
    }

    private ensureChannelListener(channel: string) {
        if (this.channelListeners.has(channel)) return;

        if (!this.eventSource) {
            // EventSource not ready yet, queue for later
            this.pendingChannels.add(channel);
            return;
        }

        this.attachChannelListener(channel);
    }

    private attachChannelListener(channel: string) {
        if (this.channelListeners.has(channel) || !this.eventSource) return;

        const listener = (e: MessageEvent) => {
            let data: BaseResponseDto;
            try {
                data = JSON.parse(e.data) as BaseResponseDto;
            } catch (err) {
                console.error(`[stream] Failed to parse event data on channel "${channel}":`, e.data);
                return;
            }

            const eventType = data.eventType;
            if (!eventType) {
                console.warn(`[stream] Received event without eventType on channel "${channel}":`, data);
                return;
            }

            let handlerFound = false;
            for (const sub of this.subscriptions.values()) {
                if (sub.channel === channel && sub.eventType === eventType) {
                    handlerFound = true;
                    try {
                        sub.handler(data);
                    } catch (err) {
                        console.error(`[stream] Handler error for ${channel}/${eventType}:`, err);
                    }
                }
            }

            if (!handlerFound && !isProd) {
                console.debug(
                    `[stream] No handler for eventType "${eventType}" on channel "${channel}". ` +
                    `Did you forget to add a handler in stream.on()?`
                );
            }
        };

        this.eventSource.addEventListener(channel, listener);
        this.channelListeners.set(channel, listener);
    }

    private maybeRemoveChannelListener(channel: string) {
        // Check if any subscription still needs this channel
        for (const sub of this.subscriptions.values()) {
            if (sub.channel === channel) return;
        }

        const listener = this.channelListeners.get(channel);
        if (listener && this.eventSource) {
            this.eventSource.removeEventListener(channel, listener);
        }
        this.channelListeners.delete(channel);
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

    // Initialize core once
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
    }, [config.url, config.connectEvent]);

    const stream: Stream = {
        connectionId,
        isConnected: connectionId !== null,
        on: (channel, eventType, handler) => coreRef.current!.on(channel, eventType, handler),
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
 * Access the stream for subscribing to SSE events.
 * Must be used within a StreamProvider.
 *
 * @example
 * const stream = useStream();
 *
 * useEffect(() => {
 *     const unsub = stream.on('myGroup', 'MessageResponseDto', (dto) => {
 *         // dto is automatically typed as MessageResponseDto
 *         console.log(dto.message);
 *     });
 *     return unsub;
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
