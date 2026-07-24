import { CallToolResultSchema, type CallToolResult } from '@modelcontextprotocol/sdk/types.js';
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
interface UnitySdkTransport {
    close(): Promise<void>;
}
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
export declare function createOfficialUnitySessionStart(options: OfficialUnitySessionOptions, dependencies?: OfficialUnitySessionDependencies): OfficialUnitySessionStart;
export {};
