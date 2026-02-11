import {StateleSSEClient} from "../../../statele-sse-client/src";

const token = localStorage.getItem('jwt');
const url = token
    ? `http://localhost:5000/sse?access_token=${token}`
    : "http://localhost:5000/sse";

export const sse = new StateleSSEClient(url);
