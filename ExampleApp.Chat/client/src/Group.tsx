import {useStream} from "./useStream.tsx";
import {useEffect, useState} from "react";
import {
    type JoinGroupResponse,
    type MessageResponseDto,
    type SendGroupMessageRequestDto,
    StringConstants
} from "./generated-ts-client.ts";
import type {GroupParams} from "./GroupParams.tsx";
import {chatClient} from "./ChatClient.tsx";

export function Group(params: GroupParams) {
    const [messages, setMessages] = useState<MessageResponseDto[]>([]);
    const [members, setMembers] = useState<string[]>([]);
    const [message, setMessage] = useState<SendGroupMessageRequestDto>({
        groupId: params.room.id,
        message: ""
    })
    const stream = useStream();
    useEffect(() => {
        const unsub1 = stream.on<JoinGroupResponse>(params.room.id!, StringConstants.JoinGroupResponse, (dto) => {
            setMembers(dto.members ?? []);
        });

        const unsub2 = stream.on<MessageResponseDto>(params.room.id!, StringConstants.MessageResponseDto, (dto) => {
            setMessages(prev => [...prev, dto]);
        });

        // dto is automatically typed as { connectionId: string; eventType: string }
        const unsub3 = stream.on<any>(params.room.id!, StringConstants.UserLeftResponseDto, (dto) => {
            setMembers(prev => prev.filter(m => m !== dto.connectionId));
        });

        return () => {
            unsub1();
            unsub2();
            unsub3();
        };
    }, [params.room.id!]);
    // Join group when connected
    useEffect(() => {
        if (!stream.connectionId) return;
        chatClient.joinGroup({connectionId: stream.connectionId, group: params.room.id});
    }, [stream.connectionId, params.room.id]);



    return (
        <div style={{border: "1px solid #ccc", padding: 10, margin: 10}}>
            Connection ID: {stream.connectionId}
            <h3>Group: {params.room.name!}</h3>
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
                Send to group {params.room.name}
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