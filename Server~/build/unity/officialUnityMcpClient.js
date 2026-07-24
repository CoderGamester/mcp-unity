import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { CallToolResultSchema, ErrorCode, McpError, } from '@modelcontextprotocol/sdk/types.js';
export class OfficialUnityMcpClient {
    options;
    sessionFactory;
    session;
    sessionPromise;
    connectionState = 'disconnected';
    closed = false;
    constructor(options) {
        this.options = {
            cliPath: options.cliPath,
            projectPath: options.projectPath,
        };
        this.sessionFactory = options.sessionFactory ?? createOfficialUnitySession;
    }
    get state() {
        return this.connectionState;
    }
    async readTool(name, args) {
        this.assertOpen();
        const firstSession = await this.getSession();
        try {
            return await firstSession.callTool(name, args);
        }
        catch (firstError) {
            if (!isTransportInterruption(firstError)) {
                throw firstError;
            }
            await this.discardSession(firstSession);
            this.assertOpen();
            const retrySession = await this.getSession();
            try {
                return await retrySession.callTool(name, args);
            }
            catch (retryError) {
                await this.discardSession(retrySession);
                throw retryError;
            }
        }
    }
    async close() {
        if (this.closed)
            return;
        this.closed = true;
        this.connectionState = 'closed';
        const pending = this.sessionPromise;
        this.sessionPromise = undefined;
        const active = this.session;
        this.session = undefined;
        if (active) {
            await active.close();
            return;
        }
        if (pending) {
            try {
                await (await pending).close();
            }
            catch {
                // Startup already failed; there is no live child to close.
            }
        }
    }
    async getSession() {
        this.assertOpen();
        if (this.session)
            return this.session;
        if (this.sessionPromise)
            return this.sessionPromise;
        this.connectionState = 'connecting';
        const pending = this.sessionFactory(this.options);
        this.sessionPromise = pending;
        try {
            const created = await pending;
            if (this.closed) {
                await created.close();
                throw new Error('Official Unity MCP client is closed.');
            }
            this.session = created;
            this.connectionState = 'connected';
            return created;
        }
        catch (error) {
            if (!this.closed) {
                this.connectionState = 'disconnected';
            }
            throw error;
        }
        finally {
            if (this.sessionPromise === pending) {
                this.sessionPromise = undefined;
            }
        }
    }
    async discardSession(candidate) {
        if (this.session === candidate) {
            this.session = undefined;
        }
        if (!this.closed) {
            this.connectionState = 'disconnected';
        }
        try {
            await candidate.close();
        }
        catch {
            // The transport is already broken; retry startup is still safe.
        }
    }
    assertOpen() {
        if (this.closed) {
            throw new Error('Official Unity MCP client is closed.');
        }
    }
}
function isTransportInterruption(error) {
    if (error instanceof McpError && error.code === ErrorCode.ConnectionClosed) {
        return true;
    }
    const code = error?.code;
    if (typeof code === 'string' &&
        ['EPIPE', 'ECONNRESET', 'ECONNREFUSED', 'ENOTCONN', 'ERR_STREAM_DESTROYED'].includes(code)) {
        return true;
    }
    const message = error instanceof Error ? error.message : String(error);
    return /^(Connection closed|Not connected)$/i.test(message.trim()) ||
        /transport (?:is )?closed/i.test(message) ||
        /Unity CLI process exited/i.test(message);
}
async function createOfficialUnitySession(options) {
    const transport = new StdioClientTransport({
        command: options.cliPath,
        args: ['mcp', '--project-path', options.projectPath],
        stderr: 'inherit',
    });
    const client = new Client({ name: 'mcp-unity-companion', version: '2.0.0' }, { capabilities: {} });
    await client.connect(transport);
    return {
        callTool: async (name, args) => {
            const result = await client.callTool({ name, arguments: args }, CallToolResultSchema);
            return CallToolResultSchema.parse(result);
        },
        close: () => client.close(),
    };
}
