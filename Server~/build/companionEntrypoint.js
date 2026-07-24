import { checkUnityCli, parseCompanionArguments, resolveUnityCliPath, } from './cli/companionCli.js';
import { installShutdownHandlers, } from './companionLifecycle.js';
import { createCompanionServer } from './companionServer.js';
import { CompanionResourceService } from './resources/companionResources.js';
import { OfficialUnityMcpClient } from './unity/officialUnityMcpClient.js';
export async function startCompanion(options) {
    const args = parseCompanionArguments(options.argv, options.isUnityProject);
    const cliPath = resolveUnityCliPath(args.unityCliPath, options.environment);
    const checked = await (options.checkCli ?? checkUnityCli)(cliPath);
    if (checked.warning) {
        options.stderr.write(`Warning: ${checked.warning}\n`);
    }
    const officialClient = new OfficialUnityMcpClient({
        cliPath: checked.command,
        projectPath: args.projectPath,
    });
    const server = createCompanionServer(new CompanionResourceService(officialClient));
    await server.connect(options.transport);
    const handlers = installShutdownHandlers({
        signals: options.signals,
        stdin: options.stdin,
        closeOfficialClient: () => officialClient.close(),
        closeServer: () => server.close(),
        onError: (error) => {
            options.stderr.write(`Shutdown error: ${errorMessage(error)}\n`);
        },
    });
    return {
        officialClient,
        shutdown: async () => {
            await handlers.shutdown();
            handlers.dispose();
        },
    };
}
function errorMessage(error) {
    return error instanceof Error ? error.message : String(error);
}
