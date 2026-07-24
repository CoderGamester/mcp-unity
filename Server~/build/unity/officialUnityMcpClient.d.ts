import type { Transport, TransportSendOptions } from '@modelcontextprotocol/sdk/shared/transport.js';
import { CallToolResultSchema, type CallToolResult, type JSONRPCMessage, type MessageExtraInfo } from '@modelcontextprotocol/sdk/types.js';
export interface OfficialUnitySession {
    callTool(name: string, args: Record<string, unknown>): Promise<CallToolResult>;
}
export interface OfficialUnitySessionStart {
    readonly ready: Promise<OfficialUnitySession>;
    close(): Promise<void>;
}
export interface OfficialUnitySessionOptions {
    cliPath: string;
    projectPath: string;
}
export type OfficialUnitySessionFactory = (options: OfficialUnitySessionOptions) => OfficialUnitySessionStart;
export type UnityConnectionState = 'disconnected' | 'connecting' | 'connected' | 'closed';
export interface OfficialUnityMcpClientOptions extends OfficialUnitySessionOptions {
    sessionFactory?: OfficialUnitySessionFactory;
}
export declare class OfficialUnityMcpClient {
    private readonly options;
    private readonly sessionFactory;
    private readonly startTeardowns;
    private readonly closeSignal;
    private signalClose;
    private active?;
    private activeStart?;
    private startupPromise?;
    private teardownPromise?;
    private closePromise?;
    private connectionState;
    private closed;
    constructor(options: OfficialUnityMcpClientOptions);
    get state(): UnityConnectionState;
    readTool(name: string, args: Record<string, unknown>): Promise<CallToolResult>;
    close(): Promise<void>;
    private getConnection;
    private discardConnection;
    private closeStart;
    private trackTeardown;
    private raceWithClose;
    private assertOpen;
}
type UnitySdkTransport = Transport;
interface UnitySdkClient {
    connect(transport: UnitySdkTransport, options: {
        signal: AbortSignal;
        timeout: number;
        maxTotalTimeout: number;
    }): Promise<void>;
    callTool(request: {
        name: string;
        arguments: Record<string, unknown>;
    }, schema: typeof CallToolResultSchema): Promise<unknown>;
    close(): Promise<void>;
}
export interface OfficialUnitySessionDependencies {
    createTransport(options: OfficialUnitySessionOptions): UnitySdkTransport;
    createClient(): UnitySdkClient;
    sdkCloseGraceMs?: number;
    transportCloseTimeoutMs?: number;
}
export declare class OwnedStdioClientTransport implements Transport {
    private readonly underlying;
    private readonly closeObservationTimeoutMs;
    onclose?: () => void;
    onerror?: (error: Error) => void;
    onmessage?: <T extends JSONRPCMessage>(message: T, extra?: MessageExtraInfo) => void;
    private readonly childClosed;
    private resolveChildClosed;
    private closePromise?;
    private startSucceeded;
    private childCloseObserved;
    private closeForwarded;
    constructor(underlying: Transport, closeObservationTimeoutMs?: number);
    get sessionId(): string | undefined;
    set sessionId(value: string | undefined);
    start(): Promise<void>;
    send(message: JSONRPCMessage, options?: TransportSendOptions): Promise<void>;
    setProtocolVersion(version: string): void;
    close(): Promise<void>;
    private closeOwnedChild;
}
export declare function createOfficialUnitySessionStart(options: OfficialUnitySessionOptions, dependencies?: OfficialUnitySessionDependencies): OfficialUnitySessionStart;
export {};
