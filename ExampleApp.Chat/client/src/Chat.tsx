import {useEffect} from "react";
import {ChatClient} from "./generated-ts-client.ts";
import {customFetch} from "./utils/customFetch.ts";

const local = "http://localhost:5000";
const prod = "https://...";

const isProd = import.meta.env.PROD;

const final = isProd ? prod : local;

const es = new EventSource(final);
const apiClient = new ChatClient(final, customFetch)

export default function Chat() {

    useEffect(() => {
        es.addEventListener("connected", dto => {
            console.log(dto.data)
            const connId = JSON.parse(dto.data).connectionId;
            localStorage.setItem('Conn', connId)
        })

        if(localStorage.getItem('Conn')==null)
            return;
        apiClient.subscribe({
            channel: "chat:123"
        }).then(r => {
            console.log(r)
        })

    }, [localStorage]);

    return <>

    </>

}