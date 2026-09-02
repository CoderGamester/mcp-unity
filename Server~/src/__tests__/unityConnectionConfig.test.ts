import { afterEach, beforeEach, describe, expect, it, jest } from '@jest/globals';
import { promises as fs } from 'fs';
import os from 'os';
import path from 'path';
import { resolveUnityConnectionConfig } from '../unity/unityConnectionConfig.js';
import { ErrorType } from '../utils/errors.js';

const TEST_TOKEN = 'a'.repeat(64);
const ALTERNATE_TOKEN = 'b'.repeat(64);

const createLogger = () => ({
  info: jest.fn(),
  warn: jest.fn()
});

describe('Unity connection configuration', () => {
  let temporaryDirectory: string;

  beforeEach(async () => {
    temporaryDirectory = await fs.mkdtemp(path.join(os.tmpdir(), 'mcp-unity-config-'));
  });

  afterEach(async () => {
    await fs.rm(temporaryDirectory, { recursive: true, force: true });
  });

  async function writeSettings(projectRoot: string, settings: object, writeToken = true): Promise<string> {
    const settingsPath = path.join(projectRoot, 'ProjectSettings', 'McpUnitySettings.json');
    await fs.mkdir(path.dirname(settingsPath), { recursive: true });
    await fs.writeFile(settingsPath, JSON.stringify(settings), 'utf8');
    if (writeToken) {
      const tokenPath = path.join(projectRoot, 'Library', 'McpUnity', 'bridge-token');
      await fs.mkdir(path.dirname(tokenPath), { recursive: true });
      await fs.writeFile(tokenPath, TEST_TOKEN, 'utf8');
    }
    return settingsPath;
  }

  it('discovers settings from the installed package path when cwd is unrelated', async () => {
    const projectRoot = path.join(temporaryDirectory, 'Unity Project');
    const settingsPath = await writeSettings(projectRoot, { Port: 9137, RequestTimeoutSeconds: 25 });
    const logger = createLogger();

    const config = await resolveUnityConnectionConfig(logger, {
      cwd: path.join(temporaryDirectory, 'unrelated-client-directory'),
      modulePath: path.join(projectRoot, 'Library', 'PackageCache', 'com.gamelovers.mcp-unity@hash', 'Server~', 'build', 'unity', 'mcpUnity.js'),
      environment: {}
    });

    expect(config).toMatchObject({ port: 9137, host: 'localhost', requestTimeout: 25000, authToken: TEST_TOKEN, settingsPath });
    expect(logger.warn).not.toHaveBeenCalled();
  });

  it('keeps Assets working-directory compatibility when the module is outside the Unity project', async () => {
    const projectRoot = path.join(temporaryDirectory, 'Project');
    await writeSettings(projectRoot, { Port: 9021, RequestTimeoutSeconds: 12 });
    const logger = createLogger();

    const config = await resolveUnityConnectionConfig(logger, {
      cwd: path.join(projectRoot, 'Assets', 'Editor'),
      modulePath: path.join(temporaryDirectory, 'global-server', 'build', 'unity', 'mcpUnity.js'),
      environment: {}
    });

    expect(config).toMatchObject({ port: 9021, requestTimeout: 12000 });
  });

  it('prefers explicit environment values over project settings', async () => {
    const projectRoot = path.join(temporaryDirectory, 'Project');
    await writeSettings(projectRoot, { Port: 9001, Host: 'settings-host', RequestTimeoutSeconds: 10 });
    const logger = createLogger();

    const config = await resolveUnityConnectionConfig(logger, {
      cwd: projectRoot,
      modulePath: path.join(temporaryDirectory, 'global-server', 'build', 'unity', 'mcpUnity.js'),
      environment: { UNITY_PORT: '9002', UNITY_HOST: 'environment-host', UNITY_REQUEST_TIMEOUT: '30' }
    });

    expect(config).toMatchObject({ port: 9002, host: 'environment-host', requestTimeout: 30000 });
    expect(logger.info).toHaveBeenCalledWith(expect.stringContaining('source: UNITY_PORT'));
  });

  it('uses an explicit settings path before module and cwd discovery', async () => {
    const firstProject = path.join(temporaryDirectory, 'first-project');
    const secondProject = path.join(temporaryDirectory, 'second-project');
    const settingsPath = await writeSettings(secondProject, { Port: 9444, RequestTimeoutSeconds: 15 });
    await writeSettings(firstProject, { Port: 9555, RequestTimeoutSeconds: 15 });
    const logger = createLogger();

    const config = await resolveUnityConnectionConfig(logger, {
      cwd: firstProject,
      modulePath: path.join(firstProject, 'Library', 'PackageCache', 'package', 'Server~', 'build', 'unity', 'mcpUnity.js'),
      environment: { MCP_UNITY_SETTINGS_PATH: settingsPath }
    });

    expect(config).toMatchObject({ port: 9444, settingsPath });
  });

  it('rejects invalid environment values and falls back to valid project settings', async () => {
    const projectRoot = path.join(temporaryDirectory, 'Project');
    await writeSettings(projectRoot, { Port: 9876, RequestTimeoutSeconds: 20 });
    const logger = createLogger();

    const config = await resolveUnityConnectionConfig(logger, {
      cwd: projectRoot,
      modulePath: path.join(temporaryDirectory, 'global-server', 'build', 'unity', 'mcpUnity.js'),
      environment: { UNITY_PORT: '9876not-a-port', UNITY_REQUEST_TIMEOUT: '5' }
    });

    expect(config).toMatchObject({ port: 9876, requestTimeout: 20000 });
    expect(logger.warn).toHaveBeenCalledWith(expect.stringContaining('UNITY_PORT must be an integer between 1 and 65535'));
    expect(logger.warn).toHaveBeenCalledWith(expect.stringContaining('UNITY_REQUEST_TIMEOUT must be an integer of at least 10'));
  });

  it('warns when it must use the default settings', async () => {
    const logger = createLogger();

    const config = await resolveUnityConnectionConfig(logger, {
      cwd: path.join(temporaryDirectory, 'no-project'),
      modulePath: path.join(temporaryDirectory, 'global-server', 'build', 'unity', 'mcpUnity.js'),
      environment: { MCP_UNITY_AUTH_TOKEN: TEST_TOKEN }
    });

    expect(config).toMatchObject({ port: 8090, host: 'localhost', requestTimeout: 10000 });
    expect(logger.warn).toHaveBeenCalledWith(expect.stringContaining('McpUnitySettings.json was not found or could not be read'));
  });

  it('prefers a direct token over token paths and project discovery', async () => {
    const projectRoot = path.join(temporaryDirectory, 'Project');
    await writeSettings(projectRoot, { Port: 8090 });
    const logger = createLogger();

    const config = await resolveUnityConnectionConfig(logger, {
      cwd: projectRoot,
      modulePath: path.join(projectRoot, 'Server~', 'build', 'index.js'),
      environment: {
        MCP_UNITY_AUTH_TOKEN: ALTERNATE_TOKEN,
        MCP_UNITY_AUTH_TOKEN_PATH: path.join(projectRoot, 'missing-token')
      }
    });

    expect(config.authToken).toBe(ALTERNATE_TOKEN);
  });

  it('reads an explicitly configured token path containing spaces', async () => {
    const tokenPath = path.join(temporaryDirectory, 'Token Directory With Spaces', 'bridge-token');
    await fs.mkdir(path.dirname(tokenPath), { recursive: true });
    await fs.writeFile(tokenPath, ALTERNATE_TOKEN, 'utf8');
    const logger = createLogger();

    const config = await resolveUnityConnectionConfig(logger, {
      cwd: temporaryDirectory,
      modulePath: path.join(temporaryDirectory, 'server', 'index.js'),
      environment: { MCP_UNITY_AUTH_TOKEN_PATH: tokenPath }
    });

    expect(config.authToken).toBe(ALTERNATE_TOKEN);
  });

  it('fails without fallback when an explicit token is malformed', async () => {
    const projectRoot = path.join(temporaryDirectory, 'Project');
    await writeSettings(projectRoot, { Port: 8090 });
    const logger = createLogger();

    await expect(resolveUnityConnectionConfig(logger, {
      cwd: projectRoot,
      modulePath: path.join(projectRoot, 'Server~', 'build', 'index.js'),
      environment: { MCP_UNITY_AUTH_TOKEN: 'not-a-token' }
    })).rejects.toMatchObject({ type: ErrorType.AUTHENTICATION });
  });

  it('fails without fallback when an explicit token path is missing', async () => {
    const projectRoot = path.join(temporaryDirectory, 'Project');
    await writeSettings(projectRoot, { Port: 8090 });
    const logger = createLogger();

    await expect(resolveUnityConnectionConfig(logger, {
      cwd: projectRoot,
      modulePath: path.join(projectRoot, 'Server~', 'build', 'index.js'),
      environment: { MCP_UNITY_AUTH_TOKEN_PATH: path.join(projectRoot, 'missing-token') }
    })).rejects.toMatchObject({ type: ErrorType.AUTHENTICATION });
  });

  it('fails closed when the project token file is missing', async () => {
    const projectRoot = path.join(temporaryDirectory, 'Project');
    await writeSettings(projectRoot, { Port: 8090 }, false);
    const logger = createLogger();

    await expect(resolveUnityConnectionConfig(logger, {
      cwd: projectRoot,
      modulePath: path.join(projectRoot, 'Server~', 'build', 'index.js'),
      environment: {}
    })).rejects.toMatchObject({ type: ErrorType.AUTHENTICATION });
  });

  it('never logs a directly configured authentication token', async () => {
    const logger = createLogger();

    await resolveUnityConnectionConfig(logger, {
      cwd: temporaryDirectory,
      modulePath: path.join(temporaryDirectory, 'server', 'index.js'),
      environment: { MCP_UNITY_AUTH_TOKEN: ALTERNATE_TOKEN }
    });

    expect(JSON.stringify(logger.info.mock.calls)).not.toContain(ALTERNATE_TOKEN);
    expect(JSON.stringify(logger.warn.mock.calls)).not.toContain(ALTERNATE_TOKEN);
  });
});
