import type { Transport } from '@modelcontextprotocol/sdk/shared/transport.js';
import {
  checkUnityCli,
  type CheckedUnityCli,
  parseCompanionArguments,
  resolveUnityCliPath,
} from './cli/companionCli.js';
import {
  installShutdownHandlers,
  type EventSource,
} from './companionLifecycle.js';
import { createCompanionServer } from './companionServer.js';
import { CompanionResourceService } from './resources/companionResources.js';
import { OfficialUnityMcpClient } from './unity/officialUnityMcpClient.js';
import { boundedErrorDetail } from './utils/boundedError.js';

export interface CompanionEntrypointOptions {
  argv: readonly string[];
  environment: NodeJS.ProcessEnv;
  isUnityProject?: (candidate: string) => boolean;
  checkCli?: (command: string) => Promise<CheckedUnityCli>;
  transport: Transport;
  signals: EventSource;
  stdin: EventSource;
  stderr: { write(text: string): unknown };
}

export interface CompanionRuntime {
  officialClient: OfficialUnityMcpClient;
  shutdown(): Promise<void>;
}

export async function startCompanion(
  options: CompanionEntrypointOptions,
): Promise<CompanionRuntime> {
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
  const server = createCompanionServer(
    new CompanionResourceService(officialClient),
  );
  await server.connect(options.transport);

  const handlers = installShutdownHandlers({
    signals: options.signals,
    stdin: options.stdin,
    closeOfficialClient: () => officialClient.close(),
    closeServer: () => server.close(),
    onError: (error) => {
      options.stderr.write(`Shutdown error: ${boundedErrorDetail(error)}\n`);
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
