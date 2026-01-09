import {Toaster} from "react-hot-toast";
import {createBrowserRouter, RouterProvider} from "react-router";
import CleanAuthExample from "./auth/CleanAuthExample.tsx";
import {UserProvider, useUser} from "../contexts/UserContext.tsx";
import KahootGameExample from "./KahootGameExample.tsx";

function AppRoutes() {
    const {user, setUser} = useUser();


    return (
        <>
            <Toaster position={"bottom-center"}/>
            <RouterProvider router={createBrowserRouter([
                {
                    path: 'auth',
                    element: <CleanAuthExample/>
                },
                {
                    path: '',
                    element: <KahootGameExample/>
                },
            ])}/>
            {
                JSON.stringify(user)
            }
        </>
    );
}

export default function Routes() {
    return (
            <UserProvider>
                <AppRoutes/>
            </UserProvider>
    );
}