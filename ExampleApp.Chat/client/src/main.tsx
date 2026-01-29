import {createRoot} from 'react-dom/client'
import Chat from "./Chat.tsx";
import {StreamProvider} from "./useStream.tsx";
import {BASE_URL} from "./utils/BASE_URL.ts";

createRoot(document.getElementById('root')!).render(
    <StreamProvider config={{
        url: `${BASE_URL}/Connect`,
        connectEvent: "ConnectionResponse",
    }}>
        <Chat/>
    </StreamProvider>
)
