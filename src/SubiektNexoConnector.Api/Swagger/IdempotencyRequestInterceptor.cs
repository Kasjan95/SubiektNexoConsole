namespace SubiektNexoConnector.Api.Swagger;

public static class IdempotencyRequestInterceptor
{
    // Swashbuckle 6 embeds this function in a JSON string inside a JS string.
    // Avoid line breaks and backslash escapes that would be decoded twice.
    public static readonly string Script = """
        function(request) {
            const method = (request.method || '').toUpperCase();
            const url = new URL(request.url, window.location.href);
            const path = url.pathname.toLowerCase();
            if (request.loadSpec || !['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)
                || url.origin !== window.location.origin
                || path === '/maintenance' || path.startsWith('/maintenance/')) {
                return request;
            }

            request.headers = request.headers || {};
            const header = Object.keys(request.headers)
                .find(name => name.toLowerCase() === 'idempotency-key') || 'Idempotency-Key';
            if (!String(request.headers[header] || '').trim()) {
                request.headers[header] = typeof crypto.randomUUID === 'function'
                    ? crypto.randomUUID()
                    : Array.from(crypto.getRandomValues(new Uint8Array(16)),
                        value => value.toString(16).padStart(2, '0')).join('');
            }
            return request;
        }
        """.ReplaceLineEndings(" ");
}
