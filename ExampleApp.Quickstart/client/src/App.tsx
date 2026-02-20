import { useEffect, useState } from 'react'

const ROOM = 'general'

type User = { id: string; name: string }
type Message = { id: number; userId?: string; content: string; createdAt: string }

export default function App() {
  const [name, setName] = useState('')
  const [user, setUser] = useState<User | null>(null)
  const [members, setMembers] = useState<User[]>([])
  const [messages, setMessages] = useState<Message[]>([])
  const [draft, setDraft] = useState('')

  useEffect(() => {
    if (!user) return

    const messagesEs = new EventSource(`/Chat/GetMessages?roomId=${ROOM}`)
    messagesEs.onmessage = e => setMessages(JSON.parse(e.data))

    const membersEs = new EventSource(`/Chat/GetMembers?roomId=${ROOM}&userId=${user.id}`)
    membersEs.onmessage = e => setMembers(JSON.parse(e.data))

    return () => { messagesEs.close(); membersEs.close() }
  }, [user])

  async function login() {
    const res = await fetch(`/Chat/Login?name=${encodeURIComponent(name)}`, { method: 'POST' })
    setUser(await res.json())
  }

  async function send() {
    if (!draft.trim() || !user) return
    await fetch(`/Chat/PostMessage?roomId=${ROOM}&content=${encodeURIComponent(draft)}&userId=${user.id}`, { method: 'POST' })
    setDraft('')
  }

  if (!user) return (
    <div>
      <input value={name} onChange={e => setName(e.target.value)} placeholder="Your name" />
      <button onClick={login}>Join</button>
    </div>
  )

  return (
    <div style={{ display: 'flex', gap: 16 }}>
      <div>
        <h3>Members</h3>
        {members.map(m => <div key={m.id}>{m.name}</div>)}
      </div>
      <div>
        <h3>Messages</h3>
        {messages.map(m => <div key={m.id}><b>{members.find(u => u.id === m.userId)?.name ?? 'anon'}</b>: {m.content}</div>)}
        <input value={draft} onChange={e => setDraft(e.target.value)} onKeyDown={e => e.key === 'Enter' && send()} placeholder="Message" />
        <button onClick={send}>Send</button>
      </div>
    </div>
  )
}
