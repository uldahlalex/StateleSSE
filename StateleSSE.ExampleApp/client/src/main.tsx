import {createRoot} from 'react-dom/client'
import Routes from "./routes/Routes.tsx";
import './index.css'
import {UserProvider} from "./contexts/UserContext.tsx";

createRoot(document.getElementById('root')!).render(
    <UserProvider>
        <Routes />
    </UserProvider>
)
