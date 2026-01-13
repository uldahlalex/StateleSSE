import {useEffect, useState} from "react";
import {subscribeMessage} from "./generated-sse-client.ts";
import type {Message} from "./generated-client.ts";


function App() {

    const [messages, setMessages] = useState<Message[]>([])

    useEffect(() => {
        const es = subscribeMessage<Message>(
            (dto) => setMessages(prev => [...prev, dto]),
            "1",
            (err) => console.error('SSE error:', err)
        );

        return () => es.close();
    }, [])

  return (
    <>
        {
            JSON.stringify(messages)
        }
    
    </>
  )
}

export default App
