export type SseClientStatus = 'connecting' | 'connected' | 'disconnected';
export declare class StateleSSEClient {
    private _eventSource;
    private _connectionId;
    private _status;
    private _generation;
    private readonly _url;
    private readonly _connectEvent;
    private readonly _subscriptions;
    private readonly _listeners;
    private readonly _groupHandlers;
    onStatusChange?: (status: SseClientStatus) => void;
    get connectionId(): string | null;
    get status(): SseClientStatus;
    constructor(url: string, connectEvent?: string);
    listen<T>(register: (connectionId: string) => Promise<{
        group: string;
        data?: T;
    }>, onData: (data: T) => void, onError?: (error: unknown) => void): () => void;
    disconnect(): void;
    private connect;
    private handleConnected;
    private executeSubscription;
    private addListener;
    private removeListener;
    private ensureGroupHandler;
    private maybeRemoveGroupHandler;
    private clearGroupHandlers;
    private setStatus;
}
