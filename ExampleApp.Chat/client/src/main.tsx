import {createRoot} from 'react-dom/client'
import Chat from "./Chat.tsx";
import {GlobalContextProvider} from "./GlobalContext.tsx";

createRoot(document.getElementById('root')!).render(
    <GlobalContextProvider>
        <Chat/>
    </GlobalContextProvider>
)
