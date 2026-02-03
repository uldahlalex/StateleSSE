import Login from "./Login.tsx";
import {useEffect, useState} from "react";
import type {Room} from "./generated-ts-client.ts";
import {chatClient} from "./ChatClient.tsx";
import {Outlet, useNavigate} from "react-router";

export default function Rooms() {

    const [rooms, setRooms] = useState<Room[]>([])
    const [createRoomForm, setCreateRoomFormm] = useState<string>("your awesome room name")
    const navigate = useNavigate();

    useEffect(() => {
        chatClient.getRooms().then(r => {
            setRooms(r)
        })
    }, []);


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
                        <button onClick={() => navigate('/room/'+r.id)}>Go to room</button>
                    </div>
                })
            }
            <hr />
            <Outlet />
        </>
    );
}

