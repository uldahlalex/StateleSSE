import {createBrowserRouter, RouterProvider} from "react-router";
import {Rooms} from "./Rooms.tsx";
import {Room} from "./Room.tsx";
import {useEffect} from "react";
import {sse} from "./Sse.tsx";
import {chatClient} from "./ChatClient.ts";

export default function Routes() {



    return <>

        <RouterProvider router={createBrowserRouter([

            {
                path: '',
                element: <Rooms />
            },
            {
                path: "room/:roomId",
                element: <Room />
            }

        ])} />
    </>
}