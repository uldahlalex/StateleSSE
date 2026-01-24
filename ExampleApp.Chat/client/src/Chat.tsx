import {useEffect, useState} from "react";
import {ChatClient} from "./generated-ts-client.ts";

const BASE_URL = 'http://localhost:5000';

const es = new EventSource(BASE_URL)
const chatClient = new ChatClient(BASE_URL)
export default function Chat() {

    const [ messages, setMessages] = useState<any[]>([])

    useEffect(() => {
        if(es && es.readyState == es.OPEN && chatClient) {
            es.addEventListener('room:123:messages', (dto) => {
                setMessages((prev) => [...prev, dto])
            })

        }

    }, []);

    return <>

        {
            JSON.stringify(messages)
        }
    </>

}