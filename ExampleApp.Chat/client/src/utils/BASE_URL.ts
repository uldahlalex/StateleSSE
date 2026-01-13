

const local = "http://localhost:5000";
const prod = "https://..."
const isProd = import.meta.env.PROD

export const BASE_URL = isProd ? prod : local;