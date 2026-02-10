export type SseClientStatus = 'connecting' | 'connected' | 'disconnected';

type Subscription = {
    register: (connectionId: string) => Promise<{ group?: string; data?: unknown }>;
    onData: (data: unknown) => void;
    onError?: (error: unknown) => void;
    listenerKey: symbol | null;
};

type Listener = {
    group: string;
    handler: (data: unknown) => void;
};

export class StateleSSEClient {
    private _eventSource: EventSource | null = null;
    private _connectionId: string | null = null;
    private _status: SseClientStatus = 'disconnected';
    private _generation = 0;
    private readonly _url: string;
    private readonly _connectEvent: string;
    private readonly _subscriptions = new Map<symbol, Subscription>();
    private readonly _listeners = new Map<symbol, Listener>();
    private readonly _groupHandlers = new Map<string, (e: MessageEvent) => void>();

    onStatusChange?: (status: SseClientStatus) => void;

    get connectionId(): string | null { return this._connectionId; }
    get status(): SseClientStatus { return this._status; }

    constructor(url: string, connectEvent = 'connected') {
        this._url = url;
        this._connectEvent = connectEvent;
        this.connect();
    }

    listen<T>(
        register: (connectionId: string) => Promise<{ group?: string | null; data?: T | null }>,
        onData: (data: T & {}) => void,
        onError?: (error: unknown) => void
    ): () => void {
        const key = Symbol();
        const sub: Subscription = {
            register: register as Subscription['register'],
            onData: onData as (data: unknown) => void,
            onError,
            listenerKey: null,
        };
        this._subscriptions.set(key, sub);

        if (this._connectionId)
            this.executeSubscription(key, this._generation);

        return () => {
            if (sub.listenerKey) this.removeListener(sub.listenerKey);
            this._subscriptions.delete(key);
        };
    }

    disconnect() {
        this._generation++;
        this._eventSource?.close();
        this._eventSource = null;
        this._connectionId = null;
        this.clearGroupHandlers();
        this._subscriptions.clear();
        this.setStatus('disconnected');
    }

    private connect() {
        this.setStatus('connecting');
        this._eventSource = new EventSource(this._url);

        this._eventSource.addEventListener(this._connectEvent, (e) => {
            const data = JSON.parse(e.data);
            this.handleConnected(data?.connectionId);
        });

        this._eventSource.onerror = () => {
            if (this._eventSource?.readyState === EventSource.CLOSED)
                this.setStatus('disconnected');
            else
                this.setStatus('connecting');
        };
    }

    private handleConnected(connectionId: string) {
        this._generation++;
        const gen = this._generation;
        this._connectionId = connectionId;

        this.clearGroupHandlers();
        for (const sub of this._subscriptions.values())
            sub.listenerKey = null;

        this.setStatus('connected');

        for (const key of this._subscriptions.keys())
            this.executeSubscription(key, gen);
    }

    private async executeSubscription(key: symbol, gen: number) {
        const sub = this._subscriptions.get(key);
        if (!sub || !this._connectionId || gen !== this._generation) return;

        try {
            const { group, data } = await sub.register(this._connectionId);
            if (gen !== this._generation || !this._subscriptions.has(key)) return;
            if (data != null) sub.onData(data);
            if (group) sub.listenerKey = this.addListener(group, sub.onData);
        } catch (e) {
            if (gen !== this._generation || !this._subscriptions.has(key)) return;
            sub.onError?.(e);
        }
    }

    private addListener(group: string, handler: (data: unknown) => void): symbol {
        const key = Symbol();
        this._listeners.set(key, { group, handler });
        this.ensureGroupHandler(group);
        return key;
    }

    private removeListener(key: symbol) {
        const listener = this._listeners.get(key);
        if (!listener) return;
        this._listeners.delete(key);
        this.maybeRemoveGroupHandler(listener.group);
    }

    private ensureGroupHandler(group: string) {
        if (this._groupHandlers.has(group) || !this._eventSource) return;

        const handler = (e: MessageEvent) => {
            let data: unknown;
            try { data = JSON.parse(e.data); } catch { return; }
            for (const listener of this._listeners.values()) {
                if (listener.group !== group) continue;
                listener.handler(data);
            }
        };

        this._eventSource.addEventListener(group, handler);
        this._groupHandlers.set(group, handler);
    }

    private maybeRemoveGroupHandler(group: string) {
        for (const listener of this._listeners.values())
            if (listener.group === group) return;

        const handler = this._groupHandlers.get(group);
        if (handler && this._eventSource)
            this._eventSource.removeEventListener(group, handler);
        this._groupHandlers.delete(group);
    }

    private clearGroupHandlers() {
        for (const [group, handler] of this._groupHandlers)
            this._eventSource?.removeEventListener(group, handler);
        this._listeners.clear();
        this._groupHandlers.clear();
    }

    private setStatus(status: SseClientStatus) {
        if (this._status === status) return;
        this._status = status;
        this.onStatusChange?.(status);
    }
}
