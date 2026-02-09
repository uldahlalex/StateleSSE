export class StateleSSEClient {
    get connectionId() { return this._connectionId; }
    get status() { return this._status; }
    constructor(url, connectEvent = 'connected') {
        this._eventSource = null;
        this._connectionId = null;
        this._status = 'disconnected';
        this._generation = 0;
        this._subscriptions = new Map();
        this._listeners = new Map();
        this._groupHandlers = new Map();
        this._url = url;
        this._connectEvent = connectEvent;
        this.connect();
    }
    listen(register, onData, onError) {
        const key = Symbol();
        const sub = {
            register: register,
            onData: onData,
            onError,
            listenerKey: null,
        };
        this._subscriptions.set(key, sub);
        if (this._connectionId)
            this.executeSubscription(key, this._generation);
        return () => {
            if (sub.listenerKey)
                this.removeListener(sub.listenerKey);
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
    connect() {
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
    handleConnected(connectionId) {
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
    async executeSubscription(key, gen) {
        const sub = this._subscriptions.get(key);
        if (!sub || !this._connectionId || gen !== this._generation)
            return;
        try {
            const { group, data } = await sub.register(this._connectionId);
            if (gen !== this._generation || !this._subscriptions.has(key))
                return;
            if (data !== undefined)
                sub.onData(data);
            sub.listenerKey = this.addListener(group, sub.onData);
        }
        catch (e) {
            if (gen !== this._generation || !this._subscriptions.has(key))
                return;
            sub.onError?.(e);
        }
    }
    addListener(group, handler) {
        const key = Symbol();
        this._listeners.set(key, { group, handler });
        this.ensureGroupHandler(group);
        return key;
    }
    removeListener(key) {
        const listener = this._listeners.get(key);
        if (!listener)
            return;
        this._listeners.delete(key);
        this.maybeRemoveGroupHandler(listener.group);
    }
    ensureGroupHandler(group) {
        if (this._groupHandlers.has(group) || !this._eventSource)
            return;
        const handler = (e) => {
            let data;
            try {
                data = JSON.parse(e.data);
            }
            catch {
                return;
            }
            for (const listener of this._listeners.values()) {
                if (listener.group !== group)
                    continue;
                listener.handler(data);
            }
        };
        this._eventSource.addEventListener(group, handler);
        this._groupHandlers.set(group, handler);
    }
    maybeRemoveGroupHandler(group) {
        for (const listener of this._listeners.values())
            if (listener.group === group)
                return;
        const handler = this._groupHandlers.get(group);
        if (handler && this._eventSource)
            this._eventSource.removeEventListener(group, handler);
        this._groupHandlers.delete(group);
    }
    clearGroupHandlers() {
        for (const [group, handler] of this._groupHandlers)
            this._eventSource?.removeEventListener(group, handler);
        this._listeners.clear();
        this._groupHandlers.clear();
    }
    setStatus(status) {
        if (this._status === status)
            return;
        this._status = status;
        this.onStatusChange?.(status);
    }
}
