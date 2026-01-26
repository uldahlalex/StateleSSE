import { useEffect, useState, useRef } from "react";
import { RealtimeClient } from "./generated-ts-client";

const BASE_URL = "http://localhost:5000";
const api = new RealtimeClient(BASE_URL);

type Event = { type: string; data: unknown; timestamp: string };

export default function RealtimeDemo() {
    const [connectionId, setConnectionId] = useState<string | null>(null);
    const [events, setEvents] = useState<Event[]>([]);
    const [myGroups, setMyGroups] = useState<string[]>([]);
    const esRef = useRef<EventSource | null>(null);

    const log = (type: string, data: unknown) => {
        setEvents((prev) => [...prev, { type, data, timestamp: new Date().toISOString() }]);
    };

    // Connect on mount
    useEffect(() => {
        const es = new EventSource(`${BASE_URL}/api/realtime/connect`);
        esRef.current = es;

        es.addEventListener("connected", (e) => {
            const data = JSON.parse(e.data);
            setConnectionId(data.connectionId);
            log("connected", data);
        });

        // Listen for group events (dynamic - we add listeners as we join groups)
        es.onmessage = (e) => {
            log("message", JSON.parse(e.data));
        };

        es.onerror = () => log("error", { message: "connection error" });

        return () => es.close();
    }, []);

    // Helper to add group-specific listener
    const addGroupListener = (group: string) => {
        esRef.current?.addEventListener(group, (e) => {
            log(`group:${group}`, JSON.parse(e.data));
        });
    };

    if (!connectionId) return <div>connecting...</div>;

    return (
        <div>
            <h3>Connection</h3>
            <pre>{connectionId}</pre>

            <h3>My Groups</h3>
            <pre>{JSON.stringify(myGroups)}</pre>

            <h3>Actions</h3>

            {/* Join Group */}
            <button onClick={async () => {
                const group = prompt("group name?") || "test-group";
                const res = await api.joinGroup({ connectionId, group });
                addGroupListener(group);
                setMyGroups((prev) => [...prev, group]);
                log("joinGroup:response", res);
            }}>
                Join Group
            </button>

            {/* Leave Group */}
            <button onClick={async () => {
                const group = prompt("group name?") || myGroups[0];
                if (!group) return;
                const res = await api.leaveGroup({ connectionId, group });
                setMyGroups((prev) => prev.filter((g) => g !== group));
                log("leaveGroup:response", res);
            }}>
                Leave Group
            </button>

            {/* Send to Group */}
            <button onClick={async () => {
                const group = prompt("group?") || myGroups[0];
                const message = prompt("message?") || "hello";
                if (!group) return;
                await api.sendToGroup({ connectionId, group, payload: { message } });
                log("sendToGroup:sent", { group, message });
            }}>
                Send to Group
            </button>

            {/* Send to Client (DM) */}
            <button onClick={async () => {
                const targetConnectionId = prompt("target connectionId?");
                const message = prompt("message?") || "hello directly";
                if (!targetConnectionId) return;
                await api.sendToClient({
                    fromConnectionId: connectionId,
                    targetConnectionId,
                    payload: { message }
                });
                log("sendToClient:sent", { targetConnectionId, message });
            }}>
                Send to Client (DM)
            </button>

            {/* Broadcast */}
            <button onClick={async () => {
                const message = prompt("broadcast message?") || "hello everyone";
                await api.broadcast({ payload: { message } });
                log("broadcast:sent", { message });
            }}>
                Broadcast to All
            </button>

            {/* Get Group Members */}
            <button onClick={async () => {
                const group = prompt("group?") || myGroups[0];
                if (!group) return;
                const res = await api.getGroupMembers(group);
                log("getGroupMembers:response", res);
            }}>
                Get Group Members
            </button>

            {/* Get My Groups */}
            <button onClick={async () => {
                const res = await api.getConnectionGroups(connectionId);
                log("getConnectionGroups:response", res);
            }}>
                Get My Groups (from server)
            </button>

            <h3>Event Log</h3>
            <pre style={{ maxHeight: 400, overflow: "auto" }}>
                {JSON.stringify(events, null, 2)}
            </pre>
        </div>
    );
}
