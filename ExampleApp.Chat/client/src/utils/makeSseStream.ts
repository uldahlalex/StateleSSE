import {ChatClient} from "../generated-ts-client.ts";
import {BASE_URL} from "./BASE_URL.ts";

export function makeSseStream<T>(
    buildCall: (client: ChatClient) => Promise<T>,
    onData: (data: T) => void,
    token?: string
): EventSource {
    let url = '';
    const urlCapture = {
        fetch: (u: RequestInfo) => { url = u as string; return Promise.reject(); }
    };
    buildCall(new ChatClient(BASE_URL, urlCapture)).catch(() => {});

    if (token) url += (url.includes('?') ? '&' : '?') + `token=${encodeURIComponent(token)}`;

    const es = new EventSource(url);
    es.onmessage = e => onData(JSON.parse(e.data) as T);
    return es;
}
