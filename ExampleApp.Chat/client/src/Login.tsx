import {chatClient} from "./ChatClient.ts";
import {useState} from "react";
import type {LoginRequest, LoginResponse} from "./generated-ts-client.ts";

export default function Login() {

    const [authForm, setAuthForm] = useState<LoginRequest>({
        password: "pass",
        username: "test"
    })

    return <div className="login-section">
        <input
            className="input"
            placeholder="username"
            value={authForm.username}
            onChange={e => setAuthForm({
                ...authForm, username : e.target.value
            })}
        />
        <input
            className="input"
            placeholder="password"
            value={authForm.password}
            type="password"
            onChange={e => setAuthForm({
                ...authForm, password : e.target.value
            })}
        />
        <div className="auth-buttons">
            <button
                className="btn btn-primary"
                onClick={() => {
                    chatClient.login(authForm).then(r => {
                        localStorage.setItem('jwt', r.token!)
                        window.location.reload()
                    })
                }}
            >
                Login
            </button>
            <button
                className="btn btn-secondary"
                onClick={() => {
                    chatClient.register(authForm).then(r => {
                        localStorage.setItem('jwt', r.token!)
                        window.location.reload()
                    })
                }}
            >
                Register
            </button>
            <button
                className="btn btn-ghost"
                onClick={() => {
                    chatClient.guestLogin().then(r => {
                        localStorage.setItem('jwt', r.token!)
                        window.location.reload()
                    })
                }}
            >
                Continue as guest
            </button>
        </div>
    </div>
}
