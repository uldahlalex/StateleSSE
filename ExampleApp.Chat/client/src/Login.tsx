import {chatClient} from "./ChatClient.tsx";
import {useState} from "react";
import type {LoginRequest, LoginResponse} from "./generated-ts-client.ts";

export default function Login() {

    const [authForm, setAuthForm] = useState<LoginRequest>({
        password: "pass",
        username: "test"
    })

    return <>

        <input placeholder="username" value={authForm.username} onChange={e => setAuthForm({
            ...authForm, username : e.target.value
        })} />
        <input placeholder="password" value={authForm.password} type="password" onChange={e => setAuthForm({
            ...authForm, password : e.target.value
        })} />
        <button onClick={() => {
            chatClient.login(authForm).then(r => {
                alert('welcome!')
                localStorage.setItem('jwt', r.token!)
            }).catch(e => {
                alert('login failed')
            })
        }}>Login</button>
        <button onClick={() => {
            chatClient.register(authForm).then(r => {
                alert('welcome!')
                localStorage.setItem('jwt', r.token!)
            }).catch(e => {
                alert('login failed')
            })
        }}>Register</button>
    </>
}