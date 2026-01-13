import {useEffect, useState} from "react";
import {streamMessages} from "./generated-sse-client.ts";
import {ChatClient, type Message} from "./generated-client.ts";
import {BASE_URL} from "./utils/BASE_URL.ts";

const client = new ChatClient(BASE_URL);


function App() {

    const [messages, setMessages] = useState<Message[]>([])

    useEffect(() => {
        const es = streamMessages<Message>(
            "2",
            (dto) => setMessages(prev => [...prev, dto]), 
            (err) => console.error('SSE error:', err)
        );

        return () => es.close();
    }, [])

  return (
    <>
        {
            JSON.stringify(messages)
        }
    <button onClick={() => {
        client.createMessage("hi", "2")
    }}>Add</button>
    </>
  )
}

export default App
