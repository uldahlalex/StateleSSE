// ========================================
// Event DTOs (matching server-side types)
// ========================================

export interface ChatMessage {
  Id: string;
  RoomId: string;
  Author: string;
  Content: string;
  Timestamp: string;
}

export interface TypingIndicator {
  RoomId: string;
  Username: string;
  IsTyping: boolean;
}

export interface PresenceUpdate {
  RoomId: string;
  Username: string;
  Online: boolean;
}

// ========================================
// Request DTOs
// ========================================

export interface SendMessageRequest {
  roomId: string;
  author: string;
  content: string;
}

export interface TypingRequest {
  roomId: string;
  username: string;
  isTyping: boolean;
}

export interface PresenceRequest {
  roomId: string;
  username: string;
  online: boolean;
}

// ========================================
// API Client
// ========================================

export class ChatApiClient {
  private baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  async sendMessage(request: SendMessageRequest): Promise<ChatMessage> {
    const response = await fetch(`${this.baseUrl}/send`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
    return response.json();
  }

  async sendTyping(request: TypingRequest): Promise<void> {
    await fetch(`${this.baseUrl}/typing`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
  }

  async updatePresence(request: PresenceRequest): Promise<void> {
    await fetch(`${this.baseUrl}/presence`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
  }
}
