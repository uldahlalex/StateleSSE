import {useEffect, useState} from "react";
import {ChatClient} from "./generated-ts-client.ts";

const BASE_URL = 'http://localhost:5000';
const channels = ['rooms/123/messages', 'rooms/123/typing', 'rooms/123/presence'];
const es = new EventSource(`${BASE_URL}?channels=${encodeURIComponent(channels.join(','))}`);




const chatClient = new ChatClient(BASE_URL)
export default function Chat() {

    const [ messages, setMessages] = useState<any[]>([])
    const [viewing, setViewing] = useState<number | undefined>(undefined)

    useEffect(() => {

        if(es.readyState!=es.OPEN)
            return;

        es.addEventListener('rooms/123/messages', (event) => {
            setMessages((prev) => [...prev, JSON.parse(event.data)]);
        });
        es.addEventListener('rooms/123/typing', (event) => {
            console.log("someone is typing")
            console.log(event)
        });
        es.addEventListener('rooms/123/presence', (event) => {
            const data = JSON.parse(event.data);
            console.log(data)
            setViewing(data.OnlineUsers)
        })

            const enterRoom = chatClient.updatePresence("123", {
                online: true,
                username: "bobby"
            })

    }, [es.readyState]);

    return <>
online users:
    {
        viewing
    }

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