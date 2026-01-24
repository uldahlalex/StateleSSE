/**
 * SseClient - A thin wrapper around EventSource that hides the connectionId.
 *
 * The connectionId is an implementation detail of the SSE backplane.
 * This client captures it automatically and attaches it to all requests
 * via the X-SSE-Connection-Id header (similar to how JWT works).
 */

type EventHandler<T> = (data: T) => void;
type StatusHandler = (status: 'connecting' | 'connected' | 'disconnected') => void;

export class SseClient {
  private eventSource: EventSource;
  private connectionId: string | null = null;
  private baseUrl: string;
  private statusHandlers: StatusHandler[] = [];
  private readyPromise: Promise<void>;
  private resolveReady!: () => void;

  constructor(eventsUrl: string, baseUrl: string) {
    this.baseUrl = baseUrl;
    this.eventSource = new EventSource(eventsUrl);

    // Create a promise that resolves when connected
    this.readyPromise = new Promise((resolve) => {
      this.resolveReady = resolve;
    });

    this.eventSource.addEventListener('connected', (e) => {
      const data = JSON.parse((e as MessageEvent).data);
      this.connectionId = data.connectionId;
      this.notifyStatus('connected');
      this.resolveReady();
    });

    this.eventSource.onerror = () => {
      this.notifyStatus('disconnected');
    };

    this.notifyStatus('connecting');
  }

  /**
   * Wait for the connection to be established.
   */
  async ready(): Promise<void> {
    return this.readyPromise;
  }

  /**
   * Subscribe to a channel. ConnectionId is attached automatically.
   */
  async subscribe(channel: string): Promise<boolean> {
    await this.ready();
    const response = await this.fetch('/api/chat/subscribe', {
      method: 'POST',
      body: JSON.stringify({ channel }),
    });
    return response.ok;
  }

  /**
   * Unsubscribe from a channel. ConnectionId is attached automatically.
   */
  async unsubscribe(channel: string): Promise<boolean> {
    await this.ready();
    const response = await this.fetch('/api/chat/unsubscribe', {
      method: 'POST',
      body: JSON.stringify({ channel }),
    });
    return response.ok;
  }

  /**
   * Listen for events on a channel.
   */
  on<T>(channel: string, handler: EventHandler<T>): () => void {
    const listener = (e: Event) => {
      const data = JSON.parse((e as MessageEvent).data) as T;
      handler(data);
    };
    this.eventSource.addEventListener(channel, listener);

    // Return unsubscribe function
    return () => this.eventSource.removeEventListener(channel, listener);
  }

  /**
   * Listen for connection status changes.
   */
  onStatus(handler: StatusHandler): () => void {
    this.statusHandlers.push(handler);
    return () => {
      this.statusHandlers = this.statusHandlers.filter(h => h !== handler);
    };
  }

  /**
   * Fetch wrapper that automatically attaches the X-SSE-Connection-Id header.
   * Use this for all API calls that need the connection context.
   */
  async fetch(path: string, init?: RequestInit): Promise<Response> {
    const headers = new Headers(init?.headers);
    headers.set('Content-Type', 'application/json');

    if (this.connectionId) {
      headers.set('X-SSE-Connection-Id', this.connectionId);
    }

    return fetch(`${this.baseUrl}${path}`, {
      ...init,
      headers,
    });
  }

  /**
   * Close the connection.
   */
  close(): void {
    this.eventSource.close();
    this.notifyStatus('disconnected');
  }

  private notifyStatus(status: 'connecting' | 'connected' | 'disconnected'): void {
    this.statusHandlers.forEach(handler => handler(status));
  }
}

/**
 * Create a new SSE client.
 */
export function createSseClient(eventsUrl: string, baseUrl: string): SseClient {
  return new SseClient(eventsUrl, baseUrl);
}
