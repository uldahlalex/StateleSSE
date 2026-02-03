import {createRoot} from 'react-dom/client'
import Rooms from "./Rooms.tsx";
import {StreamProvider} from "./useStream.tsx";
import {BASE_URL} from "./utils/BASE_URL.ts";
import Routes from "./Routes.tsx";

createRoot(document.getElementById('root')!).render(
    <StreamProvider config={{
        urlForStreamEndpoint: `${BASE_URL}/Connect`,
        connectEvent: "ConnectionResponse",
    }}>
        <Routes/>
    </StreamProvider>
)
