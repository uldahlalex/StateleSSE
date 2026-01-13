import {useEffect, useState} from "react";
import {subscribeMessage} from "./generated-sse-client.ts";
import type {Message} from "./generated-client.ts";


function App() {

    const [messages, setMessages] = useState<Message[]>([])

    useEffect(() => {
        const es = subscribeMessage<Message>((dto) => {
            setMessages(prev => [...prev, dto])
        }, { groupid: "1" });

        return () => es.close();
    }, [])

  return (
    <>
    
    </>
  )
}

export default App
