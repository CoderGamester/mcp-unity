export interface EventSource {
  on(event: string, listener: () => void): unknown;
  off(event: string, listener: () => void): unknown;
}

export interface ShutdownHandlerOptions {
  signals: EventSource;
  stdin: EventSource;
  closeOfficialClient(): Promise<void>;
  closeServer(): Promise<void>;
  onError?(error: unknown): void;
}

export interface ShutdownHandlers {
  shutdown(): Promise<void>;
  dispose(): void;
}

export function installShutdownHandlers(
  options: ShutdownHandlerOptions,
): ShutdownHandlers {
  let shutdownPromise: Promise<void> | undefined;

  const shutdown = (): Promise<void> => {
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
  const trigger = (): void => {
    void shutdown();
  };
  const bindings: Array<[EventSource, string]> = [
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
