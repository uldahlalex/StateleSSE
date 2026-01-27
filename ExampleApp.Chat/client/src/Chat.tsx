import {useEffect, useRef, useState} from "react";
import {BASE_URL} from "./utils/BASE_URL.ts";
import {ChatClient, type ConnectionResponse} from "./generated-ts-client.ts";

const chatClient = new ChatClient(BASE_URL)

export default function Chat() {

    const esRef = useRef<EventSource>(
      undefined
    )
    const [connetionId, setConnectionId] = useState<string | undefined>(undefined)


    useEffect(() => {
        const es = new EventSource(BASE_URL + "/connect");
        esRef.current = es;

        es.addEventListener("connected", (e) => {
            const data = JSON.parse(e.data) as ConnectionResponse;
            setConnectionId(data.connectionId);
        });


    }, [])


    return <>

        {
            connetionId  && <>
                <Group groupId={"abc"} connectionId={connetionId!} />
                <Group groupId={"xyz"} connectionId={connetionId!} />
            </>
        }


    </>
}

interface GroupParams {
    groupId: string;
    connectionId: string
}

export  function Group(params: GroupParams) {

    useEffect(() => {
        chatClient.joinGroup({
            connectionId: params.connectionId,
            group: params.groupId
        })
        addEventListener(params.groupId, (e) => {
            console.log(e)
        })
    }, []);

    return <>

        <button onClick={() => {
            chatClient.sendMessageToGroup({
                groupId: params.groupId,
                connectionId: params.connectionId,
                message: "hello world from "+params.connectionId
            })
        }}>Send to group {params.groupId}</button>
    </>
}