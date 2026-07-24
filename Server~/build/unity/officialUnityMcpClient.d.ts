import { type CallToolResult } from '@modelcontextprotocol/sdk/types.js';
export interface OfficialUnitySession {
    callTool(name: string, args: Record<string, unknown>): Promise<CallToolResult>;
    close(): Promise<void>;
}
export interface OfficialUnitySessionOptions {
    cliPath: string;
    projectPath: string;
}
export type OfficialUnitySessionFactory = (options: OfficialUnitySessionOptions) => Promise<OfficialUnitySession>;
export type UnityConnectionState = 'disconnected' | 'connecting' | 'connected' | 'closed';
export interface OfficialUnityMcpClientOptions extends OfficialUnitySessionOptions {
    sessionFactory?: OfficialUnitySessionFactory;
}
export declare class OfficialUnityMcpClient {
    private readonly options;
    private readonly sessionFactory;
    private session?;
    private sessionPromise?;
    private connectionState;
    private closed;
    constructor(options: OfficialUnityMcpClientOptions);
    get state(): UnityConnectionState;
    readTool(name: string, args: Record<string, unknown>): Promise<CallToolResult>;
    close(): Promise<void>;
    private getSession;
    private discardSession;
    private assertOpen;
}
