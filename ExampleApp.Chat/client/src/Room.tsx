import {useStream} from "./useStream.tsx";
import {useEffect, useState} from "react";
import {
    type MessageResponseDto, type Room,
    type SendGroupMessageRequestDto,
    type JoinGroupBroadcast,
    type PokeResponseDto,
    type UserLeftResponseDto,
    StringConstants, type ConnectionIdAndUserName
} from "./generated-ts-client.ts";
import {chatClient} from "./ChatClient.tsx";
import {useNavigate, useParams} from "react-router";
export type RoomParams = {
    roomId: string;
}

export function Room() {
    const stream = useStream();
    const navigate = useNavigate()
    const params = useParams<RoomParams>();
    const [messages, setMessages] = useState<MessageResponseDto[]>([]);
    const [members, setMembers] = useState<ConnectionIdAndUserName[]>([]);
    const [room, setRoom] = useState<Room | undefined>(undefined)
    const [message, setMessage] = useState<SendGroupMessageRequestDto>({
        groupId: params.roomId,
        message: ""
    })
    useEffect(() => {
        stream.on<PokeResponseDto>("message", StringConstants.PokeResponseDto, (dto) => {
            alert("you have been poked by: "+dto.pokedBy)
        })
        const unsub1 = stream.on<JoinGroupBroadcast>(params.roomId!, StringConstants.JoinGroupBroadcast, (dto) => {
            setMembers(dto.connectedUsers!);
        });
        const unsub2 = stream.on<MessageResponseDto>(params.roomId!, StringConstants.MessageResponseDto, (dto) => {
            setMessages(prev => [...prev, dto]);
        });
        const unsub3 = stream.on<UserLeftResponseDto>(params.roomId!, StringConstants.UserLeftResponseDto, (dto) => {
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
        return <>Loading room...</>


    return (
        <div style={{border: "1px solid #ccc", padding: 10, margin: 10}}>
            <h3>Group: {room.name!}</h3>
            <p>Members in total: {members.length}</p>
            <h2>List of members:</h2>
            {
                members.map(m => {
                    return <div key={m.connectionId}>{m.userName} <button onClick={() => {
                        chatClient.poke(m.connectionId).then(r => {
                            alert('poke sent')
                        })
                    }}>poke</button></div>
                })
            }

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