import {Group} from "./Group.tsx";
import Login from "./Login.tsx";
import {useEffect, useState} from "react";
import type {Room} from "./generated-ts-client.ts";
import {chatClient} from "./ChatClient.tsx";

export default function Chat() {

    const [rooms, setRooms] = useState<Room[]>([])

    useEffect(() => {
        chatClient.getRooms().then(r => {
            setRooms(r)
        })
    }, []);
    const [createRoomForm, setCreateRoomFormm] = useState<string>("your awesome room name")


    return (
        <>
            <Login/>
            <hr/>
            <h1>create new room</h1>
            <input onChange={e => setCreateRoomFormm(e.target.value)} value={createRoomForm}/>

            <button onClick={() => {
                chatClient.createRoom(createRoomForm).then(r => {
                    setRooms(prev => [...prev, r])
                })
            }}>Create room
            </button>
            <hr/>
            {
                rooms.map(r => {
                    return <div key={r.id}>
                        <Group room={r}/>
                    </div>
                })
            }
        </>
    );
}

