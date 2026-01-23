import { useCallback, useEffect, useRef, useState } from 'react';
import { BASE_URL } from './utils/BASE_URL';
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
// SSE Hook - Uses native EventSource
// ========================================

function useSse(roomId: string) {
  const [connectionId, setConnectionId] = useState<string | null>(null);
  const [status, setStatus] = useState<'connecting' | 'connected' | 'disconnected'>('connecting');
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [typingUsers, setTypingUsers] = useState<Set<string>>(new Set());
  const [onlineUsers, setOnlineUsers] = useState<Set<string>>(new Set());
  const eventSourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    const es = new EventSource(`${BASE_URL}/api/chat/events`);
    eventSourceRef.current = es;

    es.addEventListener('connected', (e) => {
      const data = JSON.parse(e.data);
      setConnectionId(data.connectionId);
      setStatus('connected');

      // Subscribe to all channels for this room
      const channels = [
        `chat:${roomId}:messages`,
        `chat:${roomId}:typing`,
        `chat:${roomId}:presence`,
      ];

      channels.forEach((channel) => {
        fetch(`${BASE_URL}/api/chat/subscribe`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ connectionId: data.connectionId, channel }),
        });
      });
    });

    es.addEventListener(`chat:${roomId}:messages`, (e) => {
      const msg: ChatMessage = JSON.parse(e.data);
      setMessages((prev) => [...prev, msg]);
    });

    es.addEventListener(`chat:${roomId}:typing`, (e) => {
      const data: TypingIndicator = JSON.parse(e.data);
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

    es.addEventListener(`chat:${roomId}:presence`, (e) => {
      const data: PresenceUpdate = JSON.parse(e.data);
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

    es.onerror = () => {
      setStatus('disconnected');
    };

    return () => {
      es.close();
    };
  }, [roomId]);

  return { connectionId, status, messages, typingUsers, onlineUsers };
}

// ========================================
// API Client
// ========================================

const api = {
  sendMessage: (roomId: string, author: string, content: string) =>
    fetch(`${BASE_URL}/api/chat/rooms/${roomId}/messages`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ author, content }),
    }),

  sendTyping: (roomId: string, username: string, isTyping: boolean) =>
    fetch(`${BASE_URL}/api/chat/rooms/${roomId}/typing`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, isTyping }),
    }),

  updatePresence: (roomId: string, username: string, online: boolean) =>
    fetch(`${BASE_URL}/api/chat/rooms/${roomId}/presence`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
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
        <p>Using native EventSource with StateleSSE</p>
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
  const { connectionId, status, messages, typingUsers, onlineUsers } = useSse(roomId);
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
          {connectionId && <span style={styles.connectionId}>{connectionId.slice(0, 8)}...</span>}
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
