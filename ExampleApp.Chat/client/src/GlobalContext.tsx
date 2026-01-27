import {createContext, type ReactNode, useContext, useEffect, useRef, useState} from "react";
import {BASE_URL} from "./utils/BASE_URL.ts";
import type {ConnectionResponse} from "./generated-ts-client.ts";

export interface GlobalContext {
    connectionId: string | null;
    eventSource: EventSource | null;
}

export const GlobalContext = createContext<GlobalContext>({
    connectionId: null,
    eventSource: null
})

export function GlobalContextProvider({ children }: { children: ReactNode }) {
    const [connectionId, setConnectionId] = useState<string | null>(null);
    const [eventSource, setEventSource] = useState<EventSource | null>(null);
    useEffect(() => {
        const es = new EventSource(BASE_URL + "/connect");
        setEventSource(es)

        es.addEventListener("ConnectionResponse", (e) => {
            const data = JSON.parse(e.data) as ConnectionResponse;
            console.log(data)
            if(!data.connectionId)
                throw new Error("Failed to get connection ID from server")
            setConnectionId(data.connectionId!)
        });

        return () => es.close();
    }, []);

    return (
        <GlobalContext.Provider value={{ connectionId, eventSource }}>
            {children}
        </GlobalContext.Provider>
    );
}