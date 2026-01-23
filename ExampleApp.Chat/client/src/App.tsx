import {useCallback, useEffect, useMemo, useRef, useState} from 'react';
import {StatelesseProvider, useConnection, useEventAccumulator, useEventReducer,} from '@statelesse/react';
import {ChatApiClient, type ChatMessage, type PresenceUpdate, type TypingIndicator} from './types';
import {BASE_URL} from './utils/BASE_URL';
import {styles} from "./Styles.tsx";

// ========================================
// App with Provider
// ========================================

export default function App() {
  const [username, setUsername] = useState('');
  const [joined, setJoined] = useState(false);

  if (!joined) {
    return (
      <div style={styles.loginContainer}>
        <h1>Chat Demo</h1>
        <p>Using @statelesse/react with dynamic subscriptions</p>
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

  return (
    <StatelesseProvider
      config={{
        baseUrl: `${BASE_URL}/api/chat`,
        subscribePath: '/subscribe', // Generic subscribe endpoint
      }}
    >
      <ChatRoom roomId="general" username={username} />
    </StatelesseProvider>
  );
}

// ========================================
// Chat Room Component
// ========================================

export function ChatRoom({ roomId = "general", username }: { roomId: string; username: string }) {
  const client = useMemo(() => new ChatApiClient(`${BASE_URL}/api/chat`), []);

  // Announce presence on mount
  useEffect(() => {
    client.updatePresence({ roomId, username, online: true });
    return () => {
      client.updatePresence({ roomId, username, online: false });
    };
  }, [client, roomId, username]);

  return (
    <div style={styles.chatContainer}>
      <header style={styles.header}>
        <h2>Room: #{roomId}</h2>
        <ConnectionStatus />
      </header>

      <div style={styles.mainContent}>
        <aside style={styles.sidebar}>
          <OnlineUsers roomId={roomId} />
        </aside>

        <main style={styles.chatMain}>
          <MessageList roomId={roomId} />
          <TypingStatus roomId={roomId} currentUser={username} />
          <MessageInput roomId={roomId} username={username} client={client} />
        </main>
      </div>
    </div>
  );
}

// ========================================
// Connection Status
// ========================================

export function ConnectionStatus() {
  const { status, connectionId } = useConnection();

  const statusColors: Record<string, string> = {
    connected: '#4caf50',
    connecting: '#ff9800',
    disconnected: '#f44336',
    error: '#f44336',
  };

  return (
    <div style={styles.connectionStatus}>
      <span
        style={{
          ...styles.statusDot,
          backgroundColor: statusColors[status] || '#999',
        }}
      />
      <span>{status}</span>
      {connectionId && (
        <span style={styles.connectionId}>
          {connectionId.slice(0, 8)}...
        </span>
      )}
    </div>
  );
}

// ========================================
// Message List
// ========================================

export function MessageList({ roomId }: { roomId: string }) {
  const [messages] = useEventAccumulator<ChatMessage>(`chat:${roomId}:messages`);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  return (
    <div style={styles.messageList}>
      {messages.length === 0 ? (
        <div style={styles.emptyState}>No messages yet. Say hello!</div>
      ) : (
        messages.map((msg) => (
          <div key={msg.Id} style={styles.message}>
            <strong style={styles.author}>{msg.Author}</strong>
            <span style={styles.content}>{msg.Content}</span>
            <span style={styles.timestamp}>
              {new Date(msg.Timestamp).toLocaleTimeString()}
            </span>
          </div>
        ))
      )}
      <div ref={messagesEndRef} />
    </div>
  );
}

// ========================================
// Typing Status
// ========================================

export function TypingStatus({ roomId, currentUser }: { roomId: string; currentUser: string }) {
  const typingUsers = useEventReducer<Set<string>, TypingIndicator>(
    `chat:${roomId}:typing`,
    (users, event) => {
      // Ignore own typing events
      if (event.Username === currentUser) return users;

      const next = new Set(users);
      if (event.IsTyping) {
        next.add(event.Username);
      } else {
        next.delete(event.Username);
      }
      return next;
    },
    new Set()
  );

  if (typingUsers.size === 0) return null;

  const names = [...typingUsers];
  const text =
    names.length === 1
      ? `${names[0]} is typing...`
      : `${names.join(', ')} are typing...`;

  return <div style={styles.typingIndicator}>{text}</div>;
}

// ========================================
// Online Users
// ========================================

export function OnlineUsers({ roomId }: { roomId: string }) {
  const onlineUsers = useEventReducer<Map<string, boolean>, PresenceUpdate>(
    `chat:${roomId}:presence`,
    (users, event) => {
      const next = new Map(users);
      if (event.Online) {
        next.set(event.Username, true);
      } else {
        next.delete(event.Username);
      }
      return next;
    },
    new Map()
  );

  const users = [...onlineUsers.keys()];

  return (
    <div style={styles.onlineUsers}>
      <h3>Online ({users.length})</h3>
      {users.length === 0 ? (
        <div style={styles.emptyState}>No one online</div>
      ) : (
        <ul style={styles.userList}>
          {users.map((user) => (
            <li key={user} style={styles.userItem}>
              <span style={styles.onlineDot} />
              {user}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

// ========================================
// Message Input
// ========================================

export function MessageInput({
  roomId,
  username,
  client,
}: {
  roomId: string;
  username: string;
  client: ChatApiClient;
}) {
  const [content, setContent] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const typingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const handleTyping = useCallback(() => {
    if (!isTyping) {
      setIsTyping(true);
      client.sendTyping({ roomId, username, isTyping: true });
    }

    // Reset typing timeout
    if (typingTimeoutRef.current) {
      clearTimeout(typingTimeoutRef.current);
    }

    typingTimeoutRef.current = setTimeout(() => {
      setIsTyping(false);
      client.sendTyping({ roomId, username, isTyping: false });
    }, 2000);
  }, [client, roomId, username, isTyping]);

  const handleSend = useCallback(async () => {
    if (!content.trim()) return;

    // Stop typing indicator
    if (typingTimeoutRef.current) {
      clearTimeout(typingTimeoutRef.current);
    }
    if (isTyping) {
      setIsTyping(false);
      client.sendTyping({ roomId, username, isTyping: false });
    }

    await client.sendMessage({ roomId, author: username, content });
    setContent('');
  }, [client, roomId, username, content, isTyping]);

  return (
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
  );
}

// ========================================
// Styles
// ========================================

