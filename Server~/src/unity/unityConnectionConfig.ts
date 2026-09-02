import { promises as fs } from 'fs';
import path from 'path';
import { McpUnityError, ErrorType } from '../utils/errors.js';

const DEFAULT_PORT = 8090;
const DEFAULT_HOST = 'localhost';
const DEFAULT_REQUEST_TIMEOUT_SECONDS = 10;
const SETTINGS_RELATIVE_PATH = path.join('ProjectSettings', 'McpUnitySettings.json');
const TOKEN_RELATIVE_PATH = path.join('Library', 'McpUnity', 'bridge-token');
const TOKEN_PATTERN = /^[0-9a-fA-F]{64}$/;

interface LoggerLike {
  info(message: string): void;
  warn(message: string): void;
}

interface McpUnitySettingsFile {
  Port?: unknown;
  Host?: unknown;
  RequestTimeoutSeconds?: unknown;
}

interface SettingsFileResult {
  path: string;
  settings: McpUnitySettingsFile;
}

export interface UnityConnectionConfigResolutionOptions {
  cwd?: string;
  environment?: NodeJS.ProcessEnv;
  modulePath?: string;
}

export interface ResolvedUnityConnectionConfig {
  host: string;
  port: number;
  requestTimeout: number;
  authToken: string;
  settingsPath?: string;
}

/**
 * Resolves the bridge connection settings from explicit process configuration,
 * then the Unity project settings file, and finally safe defaults.
 *
 * A package-cache install keeps Server~ below the Unity project root, so
 * searching ancestors of the executing module works even when an MCP client
 * starts Node from an unrelated working directory.
 */
export async function resolveUnityConnectionConfig(
  logger: LoggerLike,
  options: UnityConnectionConfigResolutionOptions = {}
): Promise<ResolvedUnityConnectionConfig> {
  const cwd = path.resolve(options.cwd ?? process.cwd());
  const environment = options.environment ?? process.env;
  const modulePath = path.resolve(options.modulePath ?? process.argv[1] ?? cwd);
  const settingsFile = await findSettingsFile(logger, cwd, modulePath, environment);
  const settings = settingsFile?.settings ?? {};
  const settingsSource = settingsFile ? `McpUnitySettings.json (${settingsFile.path})` : 'default settings';
  const authentication = await resolveAuthenticationToken(logger, cwd, environment, settingsFile?.path);

  const port = resolveIntegerSetting({
    environment,
    environmentName: 'UNITY_PORT',
    configurationValue: settings.Port,
    configurationName: 'Port',
    configurationSource: settingsSource,
    defaultValue: DEFAULT_PORT,
    isValid: value => value >= 1 && value <= 65535,
    invalidDescription: 'an integer between 1 and 65535',
    logger
  });

  const host = resolveStringSetting({
    environment,
    environmentName: 'UNITY_HOST',
    configurationValue: settings.Host,
    configurationName: 'Host',
    configurationSource: settingsSource,
    defaultValue: DEFAULT_HOST,
    logger
  });

  const timeoutSeconds = resolveIntegerSetting({
    environment,
    environmentName: 'UNITY_REQUEST_TIMEOUT',
    configurationValue: settings.RequestTimeoutSeconds,
    configurationName: 'RequestTimeoutSeconds',
    configurationSource: settingsSource,
    defaultValue: DEFAULT_REQUEST_TIMEOUT_SECONDS,
    isValid: value => value >= DEFAULT_REQUEST_TIMEOUT_SECONDS,
    invalidDescription: `an integer of at least ${DEFAULT_REQUEST_TIMEOUT_SECONDS}`,
    logger
  });

  logger.info(`Using port: ${port.value} for Unity WebSocket connection (source: ${port.source})`);
  logger.info(`Using host: ${host.value} for Unity WebSocket connection (source: ${host.source})`);
  logger.info(`Using request timeout: ${timeoutSeconds.value} seconds (source: ${timeoutSeconds.source})`);

  return {
    host: host.value,
    port: port.value,
    requestTimeout: timeoutSeconds.value * 1000,
    authToken: authentication.token,
    settingsPath: settingsFile?.path
  };
}

async function resolveAuthenticationToken(
  logger: LoggerLike,
  cwd: string,
  environment: NodeJS.ProcessEnv,
  settingsPath?: string
): Promise<{ token: string; source: string }> {
  if (environment.MCP_UNITY_AUTH_TOKEN !== undefined) {
    const token = environment.MCP_UNITY_AUTH_TOKEN.trim();
    validateAuthenticationToken(token, 'MCP_UNITY_AUTH_TOKEN');
    logger.info('Using MCP authentication token (source: MCP_UNITY_AUTH_TOKEN)');
    return { token, source: 'MCP_UNITY_AUTH_TOKEN' };
  }

  if (environment.MCP_UNITY_AUTH_TOKEN_PATH !== undefined) {
    const configuredPath = environment.MCP_UNITY_AUTH_TOKEN_PATH.trim();
    if (!configuredPath) {
      throw new McpUnityError(
        ErrorType.AUTHENTICATION,
        'MCP_UNITY_AUTH_TOKEN_PATH must be a non-empty file path.'
      );
    }

    const tokenPath = path.resolve(cwd, configuredPath);
    const token = await readAuthenticationToken(tokenPath, 'MCP_UNITY_AUTH_TOKEN_PATH');
    logger.info(`Using MCP authentication token file (source: MCP_UNITY_AUTH_TOKEN_PATH, path: ${tokenPath})`);
    return { token, source: tokenPath };
  }

  if (!settingsPath) {
    throw new McpUnityError(
      ErrorType.AUTHENTICATION,
      'MCP authentication token could not be located. Set MCP_UNITY_AUTH_TOKEN or MCP_UNITY_AUTH_TOKEN_PATH, or generate a client configuration from the Unity Server Window.'
    );
  }

  const projectRoot = path.dirname(path.dirname(settingsPath));
  const tokenPath = path.join(projectRoot, TOKEN_RELATIVE_PATH);
  const token = await readAuthenticationToken(tokenPath, 'the Unity project');
  logger.info(`Using MCP authentication token file (source: Unity project, path: ${tokenPath})`);
  return { token, source: tokenPath };
}

async function readAuthenticationToken(tokenPath: string, source: string): Promise<string> {
  let token: string;
  try {
    token = (await fs.readFile(tokenPath, 'utf8')).trim();
  } catch (error) {
    throw new McpUnityError(
      ErrorType.AUTHENTICATION,
      `Could not read the MCP authentication token from ${source} at ${tokenPath}. ` +
      `Open Tools > MCP Unity > Server Window in Unity, then regenerate the client configuration. ` +
      `(${error instanceof Error ? error.message : String(error)})`
    );
  }

  validateAuthenticationToken(token, `${source} at ${tokenPath}`);
  return token;
}

function validateAuthenticationToken(token: string, source: string): void {
  if (!TOKEN_PATTERN.test(token)) {
    throw new McpUnityError(
      ErrorType.AUTHENTICATION,
      `The MCP authentication token from ${source} must contain exactly 64 hexadecimal characters.`
    );
  }
}

async function findSettingsFile(
  logger: LoggerLike,
  cwd: string,
  modulePath: string,
  environment: NodeJS.ProcessEnv
): Promise<SettingsFileResult | undefined> {
  const attemptedPaths = new Set<string>();
  const explicitPath = environment.MCP_UNITY_SETTINGS_PATH;

  if (explicitPath && explicitPath.trim()) {
    const resolvedExplicitPath = path.resolve(cwd, explicitPath);
    attemptedPaths.add(resolvedExplicitPath);
    const settings = await readSettingsFile(resolvedExplicitPath, logger, 'MCP_UNITY_SETTINGS_PATH');
    if (settings) {
      return { path: resolvedExplicitPath, settings };
    }
  }

  const searchRoots = [path.dirname(modulePath), cwd];
  for (const root of searchRoots) {
    const candidatePath = await findSettingsPathFromAncestor(root, attemptedPaths);
    if (!candidatePath) {
      continue;
    }

    attemptedPaths.add(candidatePath);
    const settings = await readSettingsFile(candidatePath, logger, 'project discovery');
    if (settings) {
      return { path: candidatePath, settings };
    }
  }

  const searchLocations = Array.from(new Set(searchRoots)).join(', ');
  logger.warn(
    `McpUnitySettings.json was not found or could not be read. Searched from: ${searchLocations}. ` +
    'Using environment overrides and/or default connection settings.'
  );
  return undefined;
}

async function findSettingsPathFromAncestor(startDirectory: string, excludedPaths: Set<string>): Promise<string | undefined> {
  let directory = path.resolve(startDirectory);

  while (true) {
    const candidatePath = path.join(directory, SETTINGS_RELATIVE_PATH);
    if (!excludedPaths.has(candidatePath) && await pathExists(candidatePath)) {
      return candidatePath;
    }

    const parent = path.dirname(directory);
    if (parent === directory) {
      return undefined;
    }
    directory = parent;
  }
}

async function pathExists(candidatePath: string): Promise<boolean> {
  try {
    await fs.access(candidatePath);
    return true;
  } catch {
    return false;
  }
}

async function readSettingsFile(
  settingsPath: string,
  logger: LoggerLike,
  source: string
): Promise<McpUnitySettingsFile | undefined> {
  try {
    const content = await fs.readFile(settingsPath, 'utf-8');
    const parsed: unknown = JSON.parse(content);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      logger.warn(`Could not use McpUnitySettings.json from ${source} at ${settingsPath}: the root JSON value must be an object.`);
      return undefined;
    }
    return parsed as McpUnitySettingsFile;
  } catch (error) {
    logger.warn(
      `Could not read McpUnitySettings.json from ${source} at ${settingsPath}: ` +
      `${error instanceof Error ? error.message : String(error)}`
    );
    return undefined;
  }
}

interface IntegerSettingOptions {
  environment: NodeJS.ProcessEnv;
  environmentName: string;
  configurationValue: unknown;
  configurationName: string;
  configurationSource: string;
  defaultValue: number;
  isValid(value: number): boolean;
  invalidDescription: string;
  logger: LoggerLike;
}

function resolveIntegerSetting(options: IntegerSettingOptions): { value: number; source: string } {
  const environmentValue = options.environment[options.environmentName];
  const parsedEnvironmentValue = parseInteger(environmentValue);
  if (environmentValue !== undefined) {
    if (parsedEnvironmentValue !== undefined && options.isValid(parsedEnvironmentValue)) {
      return { value: parsedEnvironmentValue, source: options.environmentName };
    }
    options.logger.warn(`${options.environmentName} must be ${options.invalidDescription}; ignoring '${environmentValue}'.`);
  }

  const parsedConfigurationValue = parseInteger(options.configurationValue);
  if (options.configurationValue !== undefined) {
    if (parsedConfigurationValue !== undefined && options.isValid(parsedConfigurationValue)) {
      return { value: parsedConfigurationValue, source: options.configurationSource };
    }
    options.logger.warn(
      `${options.configurationName} in ${options.configurationSource} must be ${options.invalidDescription}; ` +
      `ignoring '${String(options.configurationValue)}'.`
    );
  }

  return { value: options.defaultValue, source: 'default' };
}

interface StringSettingOptions {
  environment: NodeJS.ProcessEnv;
  environmentName: string;
  configurationValue: unknown;
  configurationName: string;
  configurationSource: string;
  defaultValue: string;
  logger: LoggerLike;
}

function resolveStringSetting(options: StringSettingOptions): { value: string; source: string } {
  const environmentValue = normalizeString(options.environment[options.environmentName]);
  if (options.environment[options.environmentName] !== undefined) {
    if (environmentValue) {
      return { value: environmentValue, source: options.environmentName };
    }
    options.logger.warn(`${options.environmentName} must be a non-empty string; ignoring it.`);
  }

  const configurationValue = normalizeString(options.configurationValue);
  if (options.configurationValue !== undefined) {
    if (configurationValue) {
      return { value: configurationValue, source: options.configurationSource };
    }
    options.logger.warn(`${options.configurationName} in ${options.configurationSource} must be a non-empty string; ignoring it.`);
  }

  return { value: options.defaultValue, source: 'default' };
}

function parseInteger(value: unknown): number | undefined {
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) ? value : undefined;
  }

  if (typeof value !== 'string' || !/^\d+$/.test(value.trim())) {
    return undefined;
  }

  const parsed = Number(value.trim());
  return Number.isSafeInteger(parsed) ? parsed : undefined;
}

function normalizeString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() ? value.trim() : undefined;
}
