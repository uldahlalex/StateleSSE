import {createRoot} from 'react-dom/client'
import Routes from "./Routes.tsx";
import './styles.css';

createRoot(document.getElementById('root')!).render(
    <Routes/>
)
