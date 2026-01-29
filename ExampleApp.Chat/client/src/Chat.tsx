import {useEffect, useState} from "react";
import {BASE_URL} from "./utils/BASE_URL.ts";
import {ChatClient, type MessageResponseDto} from "./generated-ts-client.ts";
import {useStream} from "./useStream.tsx";

const chatClient = new ChatClient(BASE_URL);

export default function Chat() {
    return (
        <>
            <Group groupId={"abc"}/>
            <Group groupId={"xyz"}/>
        </>
    );
}

interface GroupParams {
    groupId: string;
}

export function Group({groupId}: GroupParams) {
    const stream = useStream();
    const [messages, setMessages] = useState<MessageResponseDto[]>([]);
    const [members, setMembers] = useState<string[]>([]);

    // Join group when connected
    useEffect(() => {
        if (!stream.connectionId) return;
        chatClient.joinGroup({connectionId: stream.connectionId, group: groupId});
    }, [stream.connectionId, groupId]);

    // Subscribe to group events
    useEffect(() => {
        // dto is automatically typed as JoinGroupResponse - no manual annotation needed!
        const unsub1 = stream.on(groupId, "JoinGroupResponse", (dto) => {
            setMembers(dto.members ?? []);
        });

        // dto is automatically typed as MessageResponseDto
        const unsub2 = stream.on(groupId, "MessageResponseDto", (dto) => {
            setMessages(prev => [...prev, dto]);
        });

        // dto is automatically typed as { connectionId: string; eventType: string }
        const unsub3 = stream.on(groupId, "UserLeftResponseDto", (dto) => {
            setMembers(prev => prev.filter(m => m !== dto.connectionId));
        });

        return () => {
            unsub1();
            unsub2();
            unsub3();
        };
    }, [groupId]);

    return (
        <div style={{border: "1px solid #ccc", padding: 10, margin: 10}}>
            Connection ID: {stream.connectionId}
            <h3>Group: {groupId}</h3>
            <p>Members: {JSON.stringify(members)} - members in total: {members.length}</p>

            <button
                onClick={() => {
                    chatClient.sendMessageToGroup({
                        groupId,
                        connectionId: stream.connectionId!,
                        message: "hello from " + stream.connectionId,
                    });
                }}
            >
                Send to group {groupId}
            </button>

            <ul>
                {messages.map((m, i) => (
                    <li key={i}>
                        <strong>{m.connectionId}:</strong> {m.message}
                    </li>
                ))}
            </ul>
        </div>
    );
}
