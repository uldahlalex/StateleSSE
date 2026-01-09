export class ValidationException extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'ValidationException';
    }
}

export function ensureDefined<T>(value: T | null | undefined, fieldName: string): T {
    if (value === null || value === undefined) {
        throw new ValidationException(`${fieldName} is required but was ${value}`);
    }
    return value;
}

export function ensureNotEmpty(value: string | null | undefined, fieldName: string): string {
    const defined = ensureDefined(value, fieldName);
    if (defined.trim().length === 0) {
        throw new ValidationException(`${fieldName} cannot be empty`);
    }
    return defined;
}

export function validateRequest<T extends Record<string, any>>(
    request: T,
    requiredFields: (keyof T)[]
): T {
    for (const field of requiredFields) {
        if (request[field] === null || request[field] === undefined) {
            throw new ValidationException(`Field '${String(field)}' is required but was ${request[field]}`);
        }
    }
    return request;
}
