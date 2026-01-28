import {useContext, useEffect, useState} from "react";
import {BASE_URL} from "./utils/BASE_URL.ts";
import {
    type BaseResponseDto,
    ChatClient,
    type JoinGroupResponse,
    type MessageResponseDto,
    StringConstants,
} from "./generated-ts-client.ts";
import {GlobalContext} from "./GlobalContext.tsx";

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

export function Group(params: GroupParams) {
    const context = useContext(GlobalContext);
    const [messages, setMessages] = useState<MessageResponseDto[]>([]);
    const [members, setMembers] = useState<string[]>([]);

    useEffect(() => {
        if (!context.connectionId || !context.eventSource)
            return;

        chatClient.joinGroup({
            connectionId: context.connectionId,
            group: params.groupId,
        });
        context.eventSource.addEventListener(params.groupId, handler);

        function handler(e) {
            const event = JSON.parse(e.data) as BaseResponseDto;
            switch (event.eventType) {
                case StringConstants.JoinGroupResponse:
                    const joined = event as JoinGroupResponse;
                    console.log(members)
                    setMembers(joined.members!);
                    break;
                case StringConstants.MessageResponseDto:
                    const msg = event as MessageResponseDto;
                    setMessages((prev) => [...prev, msg]);
                    break;
                case StringConstants.UserLeftResponseDto:
                    const l = event as any;
                    setMembers(prev => prev.filter(p => p != l.connectionId));
            }
        }

        return () => context.eventSource?.removeEventListener(params.groupId, handler);
    }, [context.connectionId, context.eventSource, params.groupId]);

    return (
        <div style={{border: "1px solid #ccc", padding: 10, margin: 10}}>
            Connection ID: {
            context.connectionId
        }
            <h3>Group: {params.groupId}</h3>
            <p>Members: {JSON.stringify(members)} - members in total: {members.length}</p>

            <button
                onClick={() => {
                    chatClient.sendMessageToGroup({
                        groupId: params.groupId,
                        connectionId: context.connectionId!,
                        message: "hello from " + context.connectionId,
                    });
                }}
            >
                Send to group {params.groupId}
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
