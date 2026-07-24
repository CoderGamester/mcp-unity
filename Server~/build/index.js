#!/usr/bin/env node
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { startCompanion } from './companionEntrypoint.js';
try {
    await startCompanion({
        argv: process.argv.slice(2),
        environment: process.env,
        transport: new StdioServerTransport(),
        signals: process,
        stdin: process.stdin,
        stderr: process.stderr,
    });
}
catch (error) {
    process.stderr.write(`MCP Unity Companion could not start: ${error instanceof Error ? error.message : String(error)}\n`);
    process.exitCode = 1;
}
