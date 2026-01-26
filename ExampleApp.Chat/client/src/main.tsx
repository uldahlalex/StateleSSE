import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import Chat from './Chat.tsx'
import RealtimeDemo from "./RealtimeDemo.tsx";

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <RealtimeDemo />
  </StrictMode>,
)
