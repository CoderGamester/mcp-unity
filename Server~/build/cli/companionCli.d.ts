export declare const CLI_DOCUMENTATION_URL = "https://docs.unity.com/en-us/unity-cli/use-unity-cli";
export interface CompanionArguments {
    projectPath: string;
    unityCliPath?: string;
}
type PathValidator = (candidate: string) => boolean;
export declare function parseCompanionArguments(argv: readonly string[], isUnityProject?: PathValidator): CompanionArguments;
export declare function resolveUnityCliPath(explicitPath: string | undefined, environment?: NodeJS.ProcessEnv): string;
export interface VersionCommandResult {
    stdout: string;
    stderr: string;
}
export type VersionRunner = (command: string, args: readonly string[]) => Promise<VersionCommandResult>;
export interface CheckedUnityCli {
    command: string;
    version: string;
    warning?: string;
}
export declare function checkUnityCli(command: string, runVersion?: VersionRunner): Promise<CheckedUnityCli>;
export {};
