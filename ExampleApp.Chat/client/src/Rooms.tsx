import Login from "./Login.tsx";
import {useEffect, useState} from "react";
import type {Room} from "./generated-ts-client.ts";
import {chatClient} from "./ChatClient.ts";
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
        <div className="app-container">
            <div className="card">
                <div className="card-header">
                    <h3 className="card-title">Authentication</h3>
                </div>
                <Login/>
            </div>

            <div className="card">
                <div className="card-header">
                    <h3 className="card-title">Create New Room</h3>
                </div>
                <div className="create-room-form">
                    <input
                        className="input"
                        onChange={e => setCreateRoomFormm(e.target.value)}
                        value={createRoomForm}
                        placeholder="Enter room name..."
                    />
                    <button
                        className="btn btn-primary"
                        onClick={() => {
                            chatClient.createRoom(createRoomForm).then(r => {
                                setRooms(prev => [...prev, r])
                            })
                        }}
                    >
                        Create Room
                    </button>
                </div>
            </div>

            <div className="card">
                <div className="card-header">
                    <h3 className="card-title">Available Rooms</h3>
                </div>
                <div className="rooms-grid">
                    {rooms.length === 0 ? (
                        <div className="empty-state">
                            <p>No rooms yet. Create one above!</p>
                        </div>
                    ) : (
                        rooms.map(r => (
                            <div className="room-item" key={r.id}>
                                <span className="room-name">{r.name || `Room ${r.id}`}</span>
                                <button
                                    className="btn btn-secondary btn-sm"
                                    onClick={() => navigate('/room/'+r.id)}
                                >
                                    Join Room
                                </button>
                            </div>
                        ))
                    )}
                </div>
            </div>

            <Outlet />
        </div>
    );
}
