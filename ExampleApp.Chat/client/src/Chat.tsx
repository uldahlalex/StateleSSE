import {useEffect, useState} from "react";
import {ChatClient} from "./generated-ts-client.ts";


const BASE_URL = 'http://localhost:5000';

let es = new EventSource(BASE_URL)
const chatClient = new ChatClient(BASE_URL)
export default function Chat() {

    const [ messages, setMessages] = useState<any[]>([])

    useEffect(() => {

        const channels = ['room:123:messages', 'room:123:typing'];
         es = new EventSource(`${BASE_URL}?channels=${encodeURIComponent(channels.join(','))}`);
        if(es && es.readyState == es.OPEN && chatClient) {
            es.addEventListener('room:123:messages', (dto) => {
                setMessages((prev) => [...prev, dto])
            })

        }

    }, [es]);

    return <>


        <button onClick={() => {
            chatClient.sendMessage("room:123", {
                author: "bob",
                content: "hi"
            }).then(r => {
                console.log(r)
            })
        }}>
            add
        </button>
        {
            JSON.stringify(messages)
        }
    </>

}