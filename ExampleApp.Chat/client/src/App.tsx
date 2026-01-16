import {useEffect, useState} from "react";
import {streamMessages} from "./generated-sse-client.ts";
import {ChatClient, type Message} from "./generated-client.ts";
import {BASE_URL} from "./utils/BASE_URL.ts";

const client = new ChatClient(BASE_URL);


function App() {

    const [messages, setMessages] = useState<Message[]>([])

    useEffect(() => {
        console.log('Connecting to SSE...');
        const es = streamMessages<Message>(
            "1",
            (dto) => {
                console.log('Received message:', dto);
                setMessages(prev => [...prev, dto]);
            },
            (err) => console.error('SSE error:', err)
        );

        es.addEventListener('open', () => console.log('SSE connection opened'));
        es.addEventListener('error', () => console.log('SSE connection error, state:', es.readyState));

        return () => {
            console.log('Closing SSE connection');
            es.close();
        };
    }, [])

  return (
    <>
        {
            JSON.stringify(messages)
        }
    <button onClick={() => {
        client.createMessage({
            content: "hi",
            groupId: "1"
        })
    }}>Add</button>
    </>
  )
}

export default App
