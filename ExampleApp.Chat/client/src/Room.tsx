import {useEffect, useRef, useState} from "react";
import {type CreateMessageRequestDto, type MemberInfo, type Message} from "./generated-ts-client.ts";
import {chatClient} from "./ChatClient.ts";
import {useParams} from "react-router";
import {makeSseStream} from "./utils/makeSseStream.ts";

export type RoomParams = {
    roomId: string;
}

export function Room() {
    const params = useParams<RoomParams>();
    const [members, setMembers] = useState<MemberInfo[]>([]);
    const [messages, setMessages] = useState<Message[]>([]);
    const [message, setMessage] = useState<CreateMessageRequestDto>({groupId: params.roomId, message: ""});
    const prevPokeCount = useRef(0);

    useEffect(() => {
        const token = localStorage.getItem('jwt') ?? undefined;

        const messagesEs = makeSseStream(c => c.getMessages(params.roomId), setMessages);
        const membersEs = makeSseStream(c => c.getMembers(params.roomId), setMembers);
        const pokesEs = token
            ? makeSseStream(c => c.getPokes(), pokes => {
                if (pokes.length > prevPokeCount.current) alert('you have been poked!');
                prevPokeCount.current = pokes.length;
            }, token)
            : null;

        return () => {
            messagesEs.close();
            membersEs.close();
            pokesEs?.close();
        };
    }, [params.roomId]);

    return (
        <div className="room-container">
            <div className="chat-panel">
                <div className="messages-container">
                    {messages.length === 0 ? (
                        <div className="empty-state"><p>No messages yet. Start the conversation!</p></div>
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
                            onChange={e => setMessage({...message, message: e.target.value})}
                            value={message.message}
                            onKeyDown={e => { if (e.key === 'Enter' && message.message?.trim()) chatClient.createMessage(message); }}
                        />
                        <button className="btn btn-primary" onClick={() => chatClient.createMessage(message)}>Send</button>
                    </div>
                </div>
            </div>

            <div className="members-panel">
                <div className="members-header"><h3>Members</h3></div>
                <div className="members-list">
                    {members.map(m => (
                        <div className="member-item" key={m.userId}>
                            <div className="member-info">
                                <div className="member-avatar">{(m.nickname || 'A').charAt(0).toUpperCase()}</div>
                                <span className="member-name">{m.nickname || 'Anonymous'}</span>
                            </div>
                            <button
                                className="btn btn-ghost btn-sm"
                                onClick={() => chatClient.poke(m.userId).then(() => alert('poke sent'))}
                            >Poke</button>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
