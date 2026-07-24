export function installShutdownHandlers(options) {
    let shutdownPromise;
    const shutdown = () => {
        shutdownPromise ??= (async () => {
            const results = await Promise.allSettled([
                options.closeOfficialClient(),
                options.closeServer(),
            ]);
            for (const result of results) {
                if (result.status === 'rejected') {
                    options.onError?.(result.reason);
                }
            }
        })();
        return shutdownPromise;
    };
    const trigger = () => {
        void shutdown();
    };
    const bindings = [
        [options.signals, 'SIGINT'],
        [options.signals, 'SIGTERM'],
        [options.stdin, 'close'],
        [options.stdin, 'end'],
    ];
    for (const [source, event] of bindings) {
        source.on(event, trigger);
    }
    return {
        shutdown,
        dispose: () => {
            for (const [source, event] of bindings) {
                source.off(event, trigger);
            }
        },
    };
}
