


export const customFetch = {
    async fetch(url: RequestInfo, init?: RequestInit): Promise<Response> {
        const token = localStorage.getItem('jwt');
        const headers = new Headers(init?.headers);

        if (token) {
            headers.set('Authorization', "Bearer " + token);
        }

        const response = await fetch(url, { ...init, headers });

        if (!response.ok) {
            const clone = response.clone();
            try {
                const problem = await clone.json();
                if (problem.detail) {
                    alert(problem.detail);
                }
            } catch {}
        }

        return response;
    }
};