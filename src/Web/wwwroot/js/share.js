export function canShare() {
    return typeof navigator.share === 'function';
}

export async function share(url) {
    try {
        await navigator.share({ url });
        return true;
    } catch (error) {
        return error.name === 'AbortError';
    }
}
