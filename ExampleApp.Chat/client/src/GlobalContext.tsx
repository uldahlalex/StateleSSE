import {createContext, type ReactNode, useContext, useEffect, useRef, useState} from "react";
import {BASE_URL} from "./utils/BASE_URL.ts";

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
    const esRef = useRef<EventSource | null>(null);

    useEffect(() => {
        const es = new EventSource(BASE_URL + "/connect");
        esRef.current = es;

        es.addEventListener("connected", (e) => {
            setConnectionId(JSON.parse(e.data).connectionId);
        });

        return () => es.close();
    }, []);

    return (
        <GlobalContext.Provider value={{ connectionId, eventSource: esRef.current }}>
            {children}
        </GlobalContext.Provider>
    );
}