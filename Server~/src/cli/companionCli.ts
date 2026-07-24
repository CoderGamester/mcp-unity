import {
  spawn,
  type ChildProcess,
  type SpawnOptions,
} from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import {
  boundedErrorDetail,
  boundedErrorMessage,
  boundedErrorText,
} from '../utils/boundedError.js';

export const CLI_DOCUMENTATION_URL =
  'https://docs.unity.com/en-us/unity-cli/use-unity-cli';

export interface CompanionArguments {
  projectPath: string;
  unityCliPath?: string;
}

type PathValidator = (candidate: string) => boolean;

export function parseCompanionArguments(
  argv: readonly string[],
  isUnityProject: PathValidator = defaultUnityProjectValidator,
): CompanionArguments {
  let projectPath: string | undefined;
  let unityCliPath: string | undefined;

  for (let index = 0; index < argv.length; index += 2) {
    const flag = argv[index];
    const value = argv[index + 1];
    if (flag !== '--project-path' && flag !== '--unity-cli-path') {
      throw new Error(
        boundedErrorMessage('Unknown argument: ', flag ?? '<empty>'),
      );
    }
    if (!value || value.startsWith('--')) {
      throw new Error(`${flag} requires a value.`);
    }

    if (flag === '--project-path') {
      if (projectPath !== undefined) {
        throw new Error('Duplicate --project-path argument.');
      }
      projectPath = value;
    } else {
      if (unityCliPath !== undefined) {
        throw new Error('Duplicate --unity-cli-path argument.');
      }
      unityCliPath = value;
    }
  }

  if (!projectPath) {
    throw new Error('--project-path <absolute-path> is required.');
  }
  if (!path.isAbsolute(projectPath)) {
    throw new Error('--project-path must be absolute.');
  }
  if (!isUnityProject(projectPath)) {
    throw new Error(
      boundedErrorMessage(
        '--project-path must identify an existing Unity project: ',
        projectPath,
      ),
    );
  }
  if (unityCliPath && !path.isAbsolute(unityCliPath)) {
    throw new Error('--unity-cli-path must be absolute.');
  }

  return { projectPath: path.resolve(projectPath), unityCliPath };
}

export function resolveUnityCliPath(
  explicitPath: string | undefined,
  environment: NodeJS.ProcessEnv = process.env,
): string {
  const environmentPath = environment.UNITY_CLI_PATH?.trim();
  return explicitPath || environmentPath || 'unity';
}

export interface VersionCommandResult {
  stdout: string;
  stderr: string;
}

export type VersionRunner = (
  command: string,
  args: readonly string[],
  options?: VersionRunOptions,
) => Promise<VersionCommandResult>;

export interface VersionRunOptions {
  timeoutMs?: number;
  signal?: AbortSignal;
}

export interface CheckedUnityCli {
  command: string;
  version: string;
  warning?: string;
}

export async function checkUnityCli(
  command: string,
  runVersion: VersionRunner = runUnityCliVersion,
  options: VersionRunOptions = {},
): Promise<CheckedUnityCli> {
  let output: VersionCommandResult;
  try {
    output = await runVersion(command, ['--version'], options);
  } catch (error) {
    throw actionableCliError(
      boundedErrorMessage(
        `Unity CLI could not be started at "${boundedErrorDetail(command)}": `,
        error,
      ),
    );
  }

  const version = parseVersion(`${output.stdout}\n${output.stderr}`);
  if (!version) {
    throw actionableCliError(
      `Unity CLI returned an unrecognized version from "${command}".`,
    );
  }

  if (compareVersion(version, MINIMUM_VERSION) < 0) {
    throw actionableCliError(
      `Unity CLI ${version.raw} is incompatible; version ${MINIMUM_VERSION.raw} or newer is required.`,
    );
  }

  return {
    command,
    version: version.raw,
    warning:
      version.major > 1n
        ? `Unity CLI ${version.raw} is newer than the tested major version 1.`
        : undefined,
  };
}

interface ParsedVersion {
  raw: string;
  major: bigint;
  minor: bigint;
  patch: bigint;
  prerelease: SemVerIdentifier[];
  build: string[];
}

type SemVerIdentifier =
  | { numeric: true; value: bigint; raw: string }
  | { numeric: false; value: string; raw: string };

const SEMVER_PATTERN =
  /^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$/;

const MINIMUM_VERSION: ParsedVersion = {
  raw: '1.0.0-beta.2',
  major: 1n,
  minor: 0n,
  patch: 0n,
  prerelease: [
    { numeric: false, value: 'beta', raw: 'beta' },
    { numeric: true, value: 2n, raw: '2' },
  ],
  build: [],
};

function parseVersion(output: string): ParsedVersion | undefined {
  for (const token of output.trim().split(/\s+/)) {
    const parsed = parseSemVerToken(token);
    if (parsed) return parsed;
  }
  return undefined;
}

function parseSemVerToken(token: string): ParsedVersion | undefined {
  const match = SEMVER_PATTERN.exec(token);
  if (!match) return undefined;
  const prereleaseTokens = match[4]?.split('.') ?? [];
  const prerelease: SemVerIdentifier[] = [];
  for (const identifier of prereleaseTokens) {
    if (/^[0-9]+$/.test(identifier)) {
      if (identifier.length > 1 && identifier.startsWith('0')) return undefined;
      prerelease.push({
        numeric: true,
        value: BigInt(identifier),
        raw: identifier,
      });
    } else {
      prerelease.push({
        numeric: false,
        value: identifier,
        raw: identifier,
      });
    }
  }
  return {
    raw: token,
    major: BigInt(match[1]),
    minor: BigInt(match[2]),
    patch: BigInt(match[3]),
    prerelease,
    build: match[5]?.split('.') ?? [],
  };
}

function compareVersion(left: ParsedVersion, right: ParsedVersion): number {
  for (const key of ['major', 'minor', 'patch'] as const) {
    if (left[key] !== right[key]) {
      return left[key] > right[key] ? 1 : -1;
    }
  }

  if (left.prerelease.length === 0 && right.prerelease.length === 0) return 0;
  if (left.prerelease.length === 0) return 1;
  if (right.prerelease.length === 0) return -1;

  const identifierCount = Math.max(
    left.prerelease.length,
    right.prerelease.length,
  );
  for (let index = 0; index < identifierCount; index++) {
    const leftIdentifier = left.prerelease[index];
    const rightIdentifier = right.prerelease[index];
    if (!leftIdentifier) return -1;
    if (!rightIdentifier) return 1;
    if (leftIdentifier.numeric && rightIdentifier.numeric) {
      if (leftIdentifier.value === rightIdentifier.value) continue;
      return leftIdentifier.value > rightIdentifier.value ? 1 : -1;
    }
    if (leftIdentifier.numeric !== rightIdentifier.numeric) {
      return leftIdentifier.numeric ? -1 : 1;
    }
    const leftValue = leftIdentifier.value as string;
    const rightValue = rightIdentifier.value as string;
    if (leftValue === rightValue) continue;
    return leftValue > rightValue ? 1 : -1;
  }
  return 0;
}

function defaultUnityProjectValidator(candidate: string): boolean {
  try {
    return (
      fs.statSync(candidate).isDirectory() &&
      fs.statSync(path.join(candidate, 'Assets')).isDirectory() &&
      fs.statSync(path.join(candidate, 'ProjectSettings')).isDirectory()
    );
  } catch {
    return false;
  }
}

type SpawnVersionProcess = (
  command: string,
  args: readonly string[],
  options: SpawnOptions,
) => Pick<ChildProcess, 'stdout' | 'stderr' | 'pid' | 'once' | 'kill'>;

export async function runUnityCliVersion(
  command: string,
  args: readonly string[],
  options: VersionRunOptions = {},
  spawnProcess: SpawnVersionProcess = spawn,
): Promise<VersionCommandResult> {
  if (args.length !== 1 || args[0] !== '--version') {
    throw new Error('Unity CLI validation may invoke only --version.');
  }
  if (options.signal?.aborted) {
    throw new Error('Unity CLI version check was cancelled.');
  }

  const timeoutMs = options.timeoutMs ?? 10_000;
  const detached = process.platform !== 'win32';
  return new Promise<VersionCommandResult>((resolve, reject) => {
    let child: ReturnType<SpawnVersionProcess>;
    try {
      child = spawnProcess(command, [...args], {
        shell: false,
        detached,
        windowsHide: true,
        stdio: ['ignore', 'pipe', 'pipe'],
      });
    } catch (error) {
      reject(error);
      return;
    }

    let stdout = '';
    let stderr = '';
    let settled = false;
    const maxOutputBytes = 64 * 1024;

    const cleanup = (): void => {
      clearTimeout(timeout);
      options.signal?.removeEventListener('abort', cancel);
    };
    const finish = (
      error?: Error,
      result?: VersionCommandResult,
    ): void => {
      if (settled) return;
      settled = true;
      cleanup();
      if (error) reject(error);
      else resolve(result ?? { stdout, stderr });
    };
    const terminate = (): void => {
      child.stdout?.destroy();
      child.stderr?.destroy();
      if (detached && child.pid) {
        try {
          process.kill(-child.pid, 'SIGKILL');
          return;
        } catch {
          // The process group may already have exited.
        }
      }
      try {
        child.kill('SIGKILL');
      } catch {
        // The child already exited.
      }
    };
    const append = (target: 'stdout' | 'stderr', chunk: unknown): void => {
      if (settled) return;
      const value = Buffer.isBuffer(chunk) ? chunk.toString('utf8') : String(chunk);
      if (target === 'stdout') stdout += value;
      else stderr += value;
      if (Buffer.byteLength(stdout) + Buffer.byteLength(stderr) > maxOutputBytes) {
        terminate();
        finish(new Error('Unity CLI version output exceeded 64 KiB.'));
      }
    };
    const cancel = (): void => {
      terminate();
      finish(new Error('Unity CLI version check was cancelled.'));
    };
    const timeout = setTimeout(() => {
      terminate();
      finish(new Error(`Unity CLI version check timed out after ${timeoutMs}ms.`));
    }, timeoutMs);

    child.stdout?.on('data', (chunk) => append('stdout', chunk));
    child.stderr?.on('data', (chunk) => append('stderr', chunk));
    child.once('error', (error) => finish(error));
    child.once('close', (code, signal) => {
      if (code === 0) {
        finish(undefined, { stdout, stderr });
      } else {
        finish(
          new Error(
            `Unity CLI --version exited with ${
              signal ? `signal ${signal}` : `code ${code ?? 'unknown'}`
            }.`,
          ),
        );
      }
    });
    if (options.signal?.aborted) {
      cancel();
    } else {
      options.signal?.addEventListener('abort', cancel, { once: true });
    }
  });
}

function actionableCliError(message: string): Error {
  return new Error(
    boundedErrorText(
      `${message} Install or update Unity CLI: ${CLI_DOCUMENTATION_URL}`,
    ),
  );
}
