import {useRealtimeListen, useStream} from "./useStream.tsx";
import {useState} from "react";
import {
    type Message, type RealtimeListenResponseOfListOfMessage,
    type Room,
    type SendGroupMessageRequestDto
} from "./generated-ts-client.ts";
import {chatClient} from "./ChatClient.ts";
import {useNavigate, useParams} from "react-router";

export type RoomParams = {
    roomId: string;
}

export function Room() {
    // const stream = useStream();
    const navigate = useNavigate()
    const params = useParams<RoomParams>();
    const [messages, setMessages] = useState<Message[]>([]);
    // const [members, setMembers] = useState<ConnectionIdAndUserName[]>([]);
    const [room, setRoom] = useState<Room | undefined>(undefined)
    const [message, setMessage] = useState<SendGroupMessageRequestDto>({
        groupId: params.roomId,
        message: ""
    })
    useRealtimeListen<Room[]>(
        (connId) => chatClient.listenToRoomMessages(connId!, params!.roomId!),
        (msgs) => setMessages(msgs),
        [params.roomId]
    );


    function sub(id: string): Promise<RealtimeListenResponseOfListOfMessage> {
        return chatClient.listenToRoomMessages(id, params.roomId!)
            .then(r => {
                setMessages(r.data!);
                return r; // must have { group, data? }
            })
            .catch(e => {
                navigate("/");
                throw e;
            });
    }

    useRealtimeListen((id) => sub(id),
        (data) => {
            setMessages(data)
        }, [params.roomId]);


    if (!room)
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
                {/*<div className="members-header">*/}
                {/*    <h3>Members</h3>*/}
                {/*    <div className="members-count">{members.length} online</div>*/}
                {/*</div>*/}
                {/*<div className="members-list">*/}
                {/*    {members.map(m => (*/}
                {/*        <div className="member-item" key={m.connectionId}>*/}
                {/*            <div className="member-info">*/}
                {/*                <div className="member-avatar">*/}
                {/*                    {(m.userName || 'A').charAt(0).toUpperCase()}*/}
                {/*                </div>*/}
                {/*                <span className="member-name">{m.userName || 'Anonymous'}</span>*/}
                {/*            </div>*/}
                {/*            <button*/}
                {/*                className="btn btn-ghost btn-sm"*/}
                {/*                onClick={() => {*/}
                {/*                    // chatClient.poke(m.connectionId).then(r => {*/}
                {/*                    //     alert('poke sent')*/}
                {/*                    // })*/}
                {/*                }}*/}
                {/*            >*/}
                {/*                Poke*/}
                {/*            </button>*/}
                {/*        </div>*/}
                {/*    ))}*/}
                {/*</div>*/}
            </div>
        </div>
    );
}
