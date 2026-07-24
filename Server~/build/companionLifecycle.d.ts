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
export declare function installShutdownHandlers(options: ShutdownHandlerOptions): ShutdownHandlers;
