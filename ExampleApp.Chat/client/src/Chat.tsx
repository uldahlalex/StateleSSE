import {useContext, useEffect, useRef, useState} from "react";
import {BASE_URL} from "./utils/BASE_URL.ts";
import {ChatClient, type ConnectionResponse, type SendGroupMessageRequestDto} from "./generated-ts-client.ts";
import {GlobalContext} from "./GlobalContext.tsx";

const chatClient = new ChatClient(BASE_URL)

export default function Chat() {




    useEffect(() => {



    }, [])


    return <>

    <>
                <Group groupId={"abc"}  />
                <Group groupId={"xyz"}  />
            </>



    </>
}

interface GroupParams {
    groupId: string;
}

export  function Group(params: GroupParams) {

    const context = useContext(GlobalContext);

    useEffect(() => {
        if(context.eventSource?.readyState != 1)
            return;
        chatClient.joinGroup({
            connectionId: context.connectionId!,
            group: params.groupId
        })
        context.eventSource!.addEventListener(params.groupId, (e) => {
            console.log(e)
            
            console.log(JSON.parse(e.data) as SendGroupMessageRequestDto)
        })
    }, [context]);

    return <>

        <button onClick={() => {
            chatClient.sendMessageToGroup({
                groupId: params.groupId,
                connectionId: context.connectionId!,
                message: "hello world from "+context.connectionId!
            })
        }}>Send to group {params.groupId}</button>
    </>
}