import { useCallback, useEffect, useRef, useState } from 'react';
import { BASE_URL } from './utils/BASE_URL';
import { SseClient, createSseClient } from './sse-client';
import { styles } from './Styles.tsx';

// ========================================
// Types
// ========================================

interface ChatMessage {
  id: string;
  roomId: string;
  author: string;
  content: string;
  timestamp: string;
}

interface TypingIndicator {
  roomId: string;
  username: string;
  isTyping: boolean;
}

interface PresenceUpdate {
  roomId: string;
  username: string;
  online: boolean;
}

// ========================================
// Global SSE Client - single connection for the app
// ========================================

const sse = createSseClient(`${BASE_URL}/api/chat/events`, BASE_URL);

// ========================================
// SSE Hook - Uses SseClient (connectionId is hidden)
// ========================================

function useSse(roomId: string) {
  const [status, setStatus] = useState<'connecting' | 'connected' | 'disconnected'>('connecting');
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [typingUsers, setTypingUsers] = useState<Set<string>>(new Set());
  const [onlineUsers, setOnlineUsers] = useState<Set<string>>(new Set());

  useEffect(() => {
    // Listen for connection status
    const unsubStatus = sse.onStatus(setStatus);

    // Subscribe to channels for this room (connectionId attached automatically)
    const channels = [
      `chat:${roomId}:messages`,
      `chat:${roomId}:typing`,
      `chat:${roomId}:presence`,
    ];

    channels.forEach((channel) => sse.subscribe(channel));

    // Listen for events on each channel
    const unsubMessages = sse.on<ChatMessage>(`chat:${roomId}:messages`, (msg) => {
      setMessages((prev) => [...prev, msg]);
    });

    const unsubTyping = sse.on<TypingIndicator>(`chat:${roomId}:typing`, (data) => {
      setTypingUsers((prev) => {
        const next = new Set(prev);
        if (data.isTyping) {
          next.add(data.username);
        } else {
          next.delete(data.username);
        }
        return next;
      });
    });

    const unsubPresence = sse.on<PresenceUpdate>(`chat:${roomId}:presence`, (data) => {
      setOnlineUsers((prev) => {
        const next = new Set(prev);
        if (data.online) {
          next.add(data.username);
        } else {
          next.delete(data.username);
        }
        return next;
      });
    });

    return () => {
      unsubStatus();
      unsubMessages();
      unsubTyping();
      unsubPresence();
      channels.forEach((channel) => sse.unsubscribe(channel));
    };
  }, [roomId]);

  return { status, messages, typingUsers, onlineUsers };
}

// ========================================
// API Client - uses sse.fetch() to auto-attach connectionId header
// ========================================

const api = {
  sendMessage: (roomId: string, author: string, content: string) =>
    sse.fetch(`/api/chat/rooms/${roomId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ author, content }),
    }),

  sendTyping: (roomId: string, username: string, isTyping: boolean) =>
    sse.fetch(`/api/chat/rooms/${roomId}/typing`, {
      method: 'POST',
      body: JSON.stringify({ username, isTyping }),
    }),

  updatePresence: (roomId: string, username: string, online: boolean) =>
    sse.fetch(`/api/chat/rooms/${roomId}/presence`, {
      method: 'POST',
      body: JSON.stringify({ username, online }),
    }),
};

// ========================================
// App
// ========================================

export default function App() {
  const [username, setUsername] = useState('');
  const [joined, setJoined] = useState(false);

  if (!joined) {
    return (
      <div style={styles.loginContainer}>
        <h1>Chat Demo</h1>
        <p>Using StateleSSE with hidden connectionId</p>
        <input
          type="text"
          placeholder="Enter your username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && username && setJoined(true)}
          style={styles.input}
        />
        <button
          onClick={() => username && setJoined(true)}
          disabled={!username}
          style={styles.button}
        >
          Join Chat
        </button>
      </div>
    );
  }

  return <ChatRoom roomId="general" username={username} />;
}

// ========================================
// Chat Room
// ========================================

function ChatRoom({ roomId, username }: { roomId: string; username: string }) {
  const { status, messages, typingUsers, onlineUsers } = useSse(roomId);
  const [content, setContent] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const typingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Announce presence when connected
  useEffect(() => {
    if (status === 'connected') {
      api.updatePresence(roomId, username, true);
      return () => {
        api.updatePresence(roomId, username, false);
      };
    }
  }, [status, roomId, username]);

  // Auto-scroll to bottom
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleTyping = useCallback(() => {
    if (!isTyping) {
      setIsTyping(true);
      api.sendTyping(roomId, username, true);
    }

    if (typingTimeoutRef.current) {
      clearTimeout(typingTimeoutRef.current);
    }

    typingTimeoutRef.current = setTimeout(() => {
      setIsTyping(false);
      api.sendTyping(roomId, username, false);
    }, 2000);
  }, [roomId, username, isTyping]);

  const handleSend = useCallback(() => {
    if (!content.trim()) return;

    if (typingTimeoutRef.current) {
      clearTimeout(typingTimeoutRef.current);
    }
    if (isTyping) {
      setIsTyping(false);
      api.sendTyping(roomId, username, false);
    }

    api.sendMessage(roomId, username, content);
    setContent('');
  }, [roomId, username, content, isTyping]);

  // Filter out own typing
  const othersTyping = [...typingUsers].filter((u) => u !== username);

  return (
    <div style={styles.chatContainer}>
      <header style={styles.header}>
        <h2>Room: #{roomId}</h2>
        <div style={styles.connectionStatus}>
          <span
            style={{
              ...styles.statusDot,
              backgroundColor: status === 'connected' ? '#4caf50' : status === 'connecting' ? '#ff9800' : '#f44336',
            }}
          />
          <span>{status}</span>
        </div>
      </header>

      <div style={styles.mainContent}>
        <aside style={styles.sidebar}>
          <div style={styles.onlineUsers}>
            <h3>Online ({onlineUsers.size})</h3>
            {onlineUsers.size === 0 ? (
              <div style={styles.emptyState}>No one online</div>
            ) : (
              <ul style={styles.userList}>
                {[...onlineUsers].map((user) => (
                  <li key={user} style={styles.userItem}>
                    <span style={styles.onlineDot} />
                    {user}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </aside>

        <main style={styles.chatMain}>
          <div style={styles.messageList}>
            {messages.length === 0 ? (
              <div style={styles.emptyState}>No messages yet. Say hello!</div>
            ) : (
              messages.map((msg) => (
                <div key={msg.id} style={styles.message}>
                  <strong style={styles.author}>{msg.author}</strong>
                  <span style={styles.content}>{msg.content}</span>
                  <span style={styles.timestamp}>{new Date(msg.timestamp).toLocaleTimeString()}</span>
                </div>
              ))
            )}
            <div ref={messagesEndRef} />
          </div>

          {othersTyping.length > 0 && (
            <div style={styles.typingIndicator}>
              {othersTyping.length === 1
                ? `${othersTyping[0]} is typing...`
                : `${othersTyping.join(', ')} are typing...`}
            </div>
          )}

          <div style={styles.inputContainer}>
            <input
              type="text"
              placeholder="Type a message..."
              value={content}
              onChange={(e) => {
                setContent(e.target.value);
                handleTyping();
              }}
              onKeyDown={(e) => e.key === 'Enter' && handleSend()}
              style={styles.messageInput}
            />
            <button onClick={handleSend} disabled={!content.trim()} style={styles.sendButton}>
              Send
            </button>
          </div>
        </main>
      </div>
    </div>
  );
}
