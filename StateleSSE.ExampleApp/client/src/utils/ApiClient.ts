import {GameEventsClient, KahootGameClient, type SseEventType} from "../generated-client.ts";
import { BASE_URL } from "./BASE_URL.ts";


export const ApiClient = new KahootGameClient(BASE_URL);