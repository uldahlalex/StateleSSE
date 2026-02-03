import {useStream} from "./useStream.tsx";
import {useEffect, useState} from "react";
import {
    type MessageResponseDto, type Room,
    type SendGroupMessageRequestDto,
    type JoinGroupBroadcast,
    StringConstants, type ConnectionIdAndUserName
} from "./generated-ts-client.ts";
import {chatClient} from "./ChatClient.tsx";
import {useParams} from "react-router";
export type RoomParams = {
    roomId: string;
}

export function Room() {
    const stream = useStream();
    const params = useParams<RoomParams>();
    const [messages, setMessages] = useState<MessageResponseDto[]>([]);
    const [members, setMembers] = useState<ConnectionIdAndUserName[]>([]);
    const [room, setRoom] = useState<Room | undefined>(undefined)
    const [message, setMessage] = useState<SendGroupMessageRequestDto>({
        groupId: params.roomId,
        message: ""
    })
    useEffect(() => {
        const unsub1 = stream.on<JoinGroupBroadcast>(params.roomId!, StringConstants.JoinGroupBroadcast, (dto) => {
            setMembers(dto.connectedUsers!);
        });
        const unsub2 = stream.on<MessageResponseDto>(params.roomId!, StringConstants.MessageResponseDto, (dto) => {
            setMessages(prev => [...prev, dto]);
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
        })
    }, [stream.connectionId, params.roomId]);

    if(!room)
        return <>Loading room...</>


    return (
        <div style={{border: "1px solid #ccc", padding: 10, margin: 10}}>
            <h3>Group: {room.name!}</h3>
            <p>Members: {JSON.stringify(members)} - members in total: {members.length}</p>

            <input onChange={e => {
                setMessage({
                    ...message, message: e.target.value
                })
            }} value={message.message} />
            <button
                onClick={() => {
                    chatClient.sendMessageToGroup(message).catch(e => {
                        alert("failed")
                    })
                }}
            >
                Send to group {room.name}
            </button>

            <ul>
                {messages.map((m, i) => (
                    <li key={i}>
                        <strong>{m.user}:</strong> {m.message}
                    </li>
                ))}
            </ul>
        </div>
    );
}