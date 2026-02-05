import {useRealtimeListen, useStream} from "./useStream.tsx";
import {useEffect, useState} from "react";
import {
    type Room,
    type SendGroupMessageRequestDto,
    type JoinGroupBroadcast,
    type PokeResponseDto,
    type UserLeftResponseDto,
    StringConstants, type ConnectionIdAndUserName
} from "./generated-ts-client.ts";
import {chatClient} from "./ChatClient.ts";
import {useNavigate, useParams} from "react-router";
export type RoomParams = {
    roomId: string;
}

export function Room() {
    const stream = useStream();
    const navigate = useNavigate()
    const params = useParams<RoomParams>();
    const [messages, setMessages] = useState<{ content?: string; user?: string }[]>([]);
    const [members, setMembers] = useState<ConnectionIdAndUserName[]>([]);
    const [room, setRoom] = useState<Room | undefined>(undefined)
    const [message, setMessage] = useState<SendGroupMessageRequestDto>({
        groupId: params.roomId,
        message: ""
    })
    useRealtimeListen<{ content?: string; user?: string }[]>(
        (connId) => chatClient.listenToRoomMessages(connId, params.roomId!),
        (msgs) => setMessages(msgs),
        [params.roomId]
    );

    useEffect(() => {
        stream.on<PokeResponseDto>("message", StringConstants.PokeResponseDto, (dto) => {
            alert("you have been poked by: "+dto.pokedBy)
        })
        const unsub1 = stream.on<JoinGroupBroadcast>(
            params.roomId!,
            "JoinGroupBroadcast",
            (dto) => {
            setMembers(dto.connectedUsers!);
        });
        const unsub2 = stream.on<UserLeftResponseDto>(params.roomId!, StringConstants.UserLeftResponseDto, (dto) => {
            setMembers(prev => [...prev.filter(u => u.connectionId == dto.connectionId)]);
        });
        return () => {
            unsub1();
            unsub2();
        };
    }, [params.roomId!]);
    useEffect(() => {
        if (!stream.connectionId)
            return;
        chatClient.joinGroup({connectionId: stream.connectionId, group: params.roomId})
            .then(r => {
            setRoom(r.room)
        }).catch(e => {
            navigate("/")
        })
    }, [stream.connectionId, params.roomId]);

    if(!room)
        return <div className="loading">Loading room...</div>


    return (
        <div className="room-container">
            <div className="chat-panel">
                <div className="chat-header">
                    <h2>{room.name!}</h2>
                </div>

                <div className="messages-container">
                    {messages.length === 0 ? (
                        <div className="empty-state">
                            <p>No messages yet. Start the conversation!</p>
                        </div>
                    ) : (
                        messages.map((m, i) => (
                            <div className="message" key={i}>
                                <div className="message-author">{m.user || 'Anonymous'}</div>
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
                                    chatClient.sendMessageToGroup(message)
                                }
                            }}
                        />
                        <button
                            className="btn btn-primary"
                            onClick={() => {
                                chatClient.sendMessageToGroup(message)
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
                    <div className="members-count">{members.length} online</div>
                </div>
                <div className="members-list">
                    {members.map(m => (
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
