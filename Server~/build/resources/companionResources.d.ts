import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
export interface UnityReadClient {
    readTool(name: string, args: Record<string, unknown>): Promise<CallToolResult>;
}
export interface CompanionResourcePayload {
    uri: string;
    payload: Record<string, unknown>;
}
export declare class CompanionResourceService {
    private readonly client;
    constructor(client: UnityReadClient);
    read(uri: string): Promise<CompanionResourcePayload>;
    private call;
}
export declare function decodeToolPayload(command: string, result: CallToolResult): Record<string, unknown>;
