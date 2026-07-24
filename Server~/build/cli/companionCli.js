import { execFile } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { promisify } from 'node:util';
export const CLI_DOCUMENTATION_URL = 'https://docs.unity.com/en-us/unity-cli/use-unity-cli';
export function parseCompanionArguments(argv, isUnityProject = defaultUnityProjectValidator) {
    let projectPath;
    let unityCliPath;
    for (let index = 0; index < argv.length; index += 2) {
        const flag = argv[index];
        const value = argv[index + 1];
        if (flag !== '--project-path' && flag !== '--unity-cli-path') {
            throw new Error(`Unknown argument: ${flag ?? '<empty>'}`);
        }
        if (!value || value.startsWith('--')) {
            throw new Error(`${flag} requires a value.`);
        }
        if (flag === '--project-path') {
            if (projectPath !== undefined) {
                throw new Error('Duplicate --project-path argument.');
            }
            projectPath = value;
        }
        else {
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
        throw new Error(`--project-path must identify an existing Unity project: ${projectPath}`);
    }
    return { projectPath: path.resolve(projectPath), unityCliPath };
}
export function resolveUnityCliPath(explicitPath, environment = process.env) {
    return explicitPath || environment.UNITY_CLI_PATH || 'unity';
}
export async function checkUnityCli(command, runVersion = defaultVersionRunner) {
    let output;
    try {
        output = await runVersion(command, ['--version']);
    }
    catch (error) {
        throw actionableCliError(`Unity CLI could not be started at "${command}": ${errorMessage(error)}`);
    }
    const version = parseVersion(`${output.stdout}\n${output.stderr}`);
    if (!version) {
        throw actionableCliError(`Unity CLI returned an unrecognized version from "${command}".`);
    }
    if (compareVersion(version, MINIMUM_VERSION) < 0) {
        throw actionableCliError(`Unity CLI ${version.raw} is incompatible; version ${MINIMUM_VERSION.raw} or newer is required.`);
    }
    return {
        command,
        version: version.raw,
        warning: version.major > 1n
            ? `Unity CLI ${version.raw} is newer than the tested major version 1.`
            : undefined,
    };
}
const VERSION_PATTERN = /(?:^|[^0-9])([0-9]+)\.([0-9]+)\.([0-9]+)(?:-(alpha|beta|rc)\.([0-9]+))?(?:\+[0-9A-Za-z.-]+)?(?:$|[^0-9A-Za-z.+-])/i;
const MINIMUM_VERSION = {
    raw: '1.0.0-beta.2',
    major: 1n,
    minor: 0n,
    patch: 0n,
    prerelease: { label: 'beta', number: 2n },
};
function parseVersion(output) {
    const match = VERSION_PATTERN.exec(output);
    if (!match) {
        return undefined;
    }
    const prereleaseLabel = match[4]?.toLowerCase();
    const raw = `${match[1]}.${match[2]}.${match[3]}${prereleaseLabel ? `-${prereleaseLabel}.${match[5]}` : ''}`;
    return {
        raw,
        major: BigInt(match[1]),
        minor: BigInt(match[2]),
        patch: BigInt(match[3]),
        prerelease: prereleaseLabel
            ? { label: prereleaseLabel, number: BigInt(match[5]) }
            : undefined,
    };
}
function compareVersion(left, right) {
    for (const key of ['major', 'minor', 'patch']) {
        if (left[key] !== right[key]) {
            return left[key] > right[key] ? 1 : -1;
        }
    }
    if (!left.prerelease && !right.prerelease)
        return 0;
    if (!left.prerelease)
        return 1;
    if (!right.prerelease)
        return -1;
    const order = { alpha: 0, beta: 1, rc: 2 };
    if (left.prerelease.label !== right.prerelease.label) {
        return order[left.prerelease.label] > order[right.prerelease.label] ? 1 : -1;
    }
    if (left.prerelease.number === right.prerelease.number)
        return 0;
    return left.prerelease.number > right.prerelease.number ? 1 : -1;
}
function defaultUnityProjectValidator(candidate) {
    try {
        return (fs.statSync(candidate).isDirectory() &&
            fs.statSync(path.join(candidate, 'Assets')).isDirectory() &&
            fs.statSync(path.join(candidate, 'ProjectSettings')).isDirectory());
    }
    catch {
        return false;
    }
}
async function defaultVersionRunner(command, args) {
    const result = await promisify(execFile)(command, [...args], {
        encoding: 'utf8',
        timeout: 10_000,
    });
    return { stdout: result.stdout, stderr: result.stderr };
}
function actionableCliError(message) {
    return new Error(`${message} Install or update Unity CLI: ${CLI_DOCUMENTATION_URL}`);
}
function errorMessage(error) {
    return error instanceof Error ? error.message : String(error);
}
