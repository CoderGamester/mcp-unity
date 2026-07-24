import type { Transport } from '@modelcontextprotocol/sdk/shared/transport.js';
import { type CheckedUnityCli } from './cli/companionCli.js';
import { type EventSource } from './companionLifecycle.js';
import { OfficialUnityMcpClient } from './unity/officialUnityMcpClient.js';
export interface CompanionEntrypointOptions {
    argv: readonly string[];
    environment: NodeJS.ProcessEnv;
    isUnityProject?: (candidate: string) => boolean;
    checkCli?: (command: string) => Promise<CheckedUnityCli>;
    transport: Transport;
    signals: EventSource;
    stdin: EventSource;
    stderr: {
        write(text: string): unknown;
    };
}
export interface CompanionRuntime {
    officialClient: OfficialUnityMcpClient;
    shutdown(): Promise<void>;
}
export declare function startCompanion(options: CompanionEntrypointOptions): Promise<CompanionRuntime>;
