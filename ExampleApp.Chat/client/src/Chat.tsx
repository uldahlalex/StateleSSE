import {useEffect, useState} from "react";
import {ChatClient} from "./generated-ts-client.ts";


const BASE_URL = 'http://localhost:5000';

const chatClient = new ChatClient(BASE_URL)
export default function Chat() {

    const [ messages, setMessages] = useState<any[]>([])

    useEffect(() => {
        const channels = ['rooms/123/messages', 'rooms/123/typing'];
        const es = new EventSource(`${BASE_URL}?channels=${encodeURIComponent(channels.join(','))}`);

        es.addEventListener('rooms/123/messages', (event) => {
            setMessages((prev) => [...prev, JSON.parse(event.data)]);
        });
        es.addEventListener('rooms/123/typing', (event) => {
            console.log("someone is typing")
            console.log(event)
        });

        return () => es.close();
    }, []);

    return <>

        <input onChange={e => {
            chatClient.sendTyping(
                "123",
            {
                username: "bob",
                    isTyping: true
            }
            ).then(r => {

            })
        }} />

        <button onClick={() => {
            chatClient.sendMessage("123", {
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