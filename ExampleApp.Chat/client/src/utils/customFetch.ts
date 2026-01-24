

/**
 * This fetch http client attaches JWT from localstorage
 * and toasts if http requests fail.
 * Note: circular reference resolution is handled at the API client level,
 * not here, because JSON.stringify() cannot preserve circular references.
 */
export const customFetch = {
    fetch(url: RequestInfo, init?: RequestInit): Promise<Response> {
        const token = localStorage.getItem('jwt');
        const headers = new Headers(init?.headers);

        if (token) {
            headers.set('Authorization', token);
        }

        const connId = localStorage.getItem('Conn')
        if(connId)
            headers.set("Conn", connId)

        return fetch(url, {
            ...init,
            headers
        }).then(async (response) => {
            // Handle errors by reading from one clone
            if (!response.ok) {
                const errorClone = response.clone();
                console.log(errorClone)
            }

            return response;
        });
    }
};