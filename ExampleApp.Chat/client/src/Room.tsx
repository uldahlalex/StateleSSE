import {useState} from "react";
import {type CreateMessageRequestDto, type Message,} from "./generated-ts-client.ts";
import {chatClient} from "./ChatClient.ts";
import {useNavigate, useParams} from "react-router";

export type RoomParams = {
    roomId: string;
}

export function Room() {
    const navigate = useNavigate()
    const params = useParams<RoomParams>();
    const [members, setMembers] = useState<any[]>([]);
    const [messages, setMessages] = useState<Message[]>([]);
    const [message, setMessage] = useState<CreateMessageRequestDto>({
        groupId: params.roomId,
        message: ""
    })


//todo get messages, pokes and group members realtime


    return (
        <div className="room-container">
            <div className="chat-panel">


                <div className="messages-container">
                    {messages.length === 0 ? (
                        <div className="empty-state">
                            <p>No messages yet. Start the conversation!</p>
                        </div>
                    ) : (
                        messages.map((m, i) => (
                            <div className="message" key={i}>
                                <div className="message-author">{m.user?.nickname || 'Anonymous'}</div>
                                <div className="message-content">{m.content}</div>
                            </div>
                        ))
                    )}
                </div>

                <div className="chat-input-container">
                    <div className="chat-input-form">
                        <input
                            className="input"
                            placeholder="Type a message..."
                            onChange={e => {
                                setMessage({
                                    ...message, message: e.target.value
                                })
                            }}
                            value={message.message}
                            onKeyDown={e => {
                                if (e.key === 'Enter' && message.message?.trim()) {
                                    chatClient.createMessage(message)
                                }
                            }}
                        />
                        <button
                            className="btn btn-primary"
                            onClick={() => {
                                chatClient.createMessage(message)
                            }}
                        >
                            Send
                        </button>
                    </div>
                </div>
            </div>

            <div className="members-panel">
                <div className="members-header">
                    <h3>Members</h3>
                </div>
                <div className="members-list">
                    {members && members.map(m => (
                        <div className="member-item" key={m.connectionId}>
                            <div className="member-info">
                                <div className="member-avatar">
                                    {(m.userName || 'A').charAt(0).toUpperCase()}
                                </div>
                                <span className="member-name">{m.userName || 'Anonymous'}</span>
                            </div>
                            <button
                                className="btn btn-ghost btn-sm"
                                onClick={() => {
                                    chatClient.poke(m.connectionId).then(r => {
                                        alert('poke sent')
                                    })
                                }}
                            >
                                Poke
                            </button>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
