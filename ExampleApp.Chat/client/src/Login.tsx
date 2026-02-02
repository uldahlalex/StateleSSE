import {chatClient} from "./ChatClient.tsx";

export default function Login() {
    return <>
        <button onClick={() => {
            chatClient.login({
                password: "test",
                username: "test"
            }).then(r => {
                localStorage.setItem('jwt', r.token!)
            }).catch(e => {
                alert('login failed')
            })
        }}>Click to login as test user</button>
    </>
}