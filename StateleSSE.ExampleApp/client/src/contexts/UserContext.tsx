import {type ReactNode, useEffect} from "react";
import {createContext, useContext, useState} from "react";
import type {UserReturnDto} from "../generated-client.ts";
import {decodeJwt} from "../utils/jwtHelper.ts";


type UserContextValue = {
    user: UserReturnDto | null;
    setUser: (user: UserReturnDto | null) => void;
    logout: () => void;
};

const UserContext = createContext<UserContextValue | null>(null);

export function UserProvider({children}: { children: ReactNode }) {
    const [user, setUser] = useState<UserReturnDto | null>(null);
    
    useEffect(() => {

        if(localStorage.getItem('jwt'))
            setUser(decodeJwt(localStorage.getItem('jwt')!))

    }, [])
    const logout = () => {
        localStorage.removeItem('jwt');
        setUser(null);
    };

    return (
        <UserContext.Provider value={{user, setUser, logout}}>
            {children}
        </UserContext.Provider>
    );
}

export function useUser() {
    const context = useContext(UserContext);
    if (!context) {
        throw new Error("useUser must be used within UserProvider");
    }
    return context;
}
