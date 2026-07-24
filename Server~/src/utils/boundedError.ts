export const ERROR_DETAIL_BUDGET_BYTES = 4 * 1024;
export const ERROR_TRUNCATION_MARKER = ' [truncated]';

export function boundedErrorDetail(error: unknown): string {
  return boundedErrorText(
    error instanceof Error ? error.message : String(error),
  );
}

export function boundedErrorMessage(
  prefix: string,
  detail?: unknown,
): string {
  return boundedErrorParts(
    detail === undefined
      ? [prefix]
      : [
          prefix,
          detail instanceof Error ? detail.message : String(detail),
        ],
  );
}

export function boundedError(error: unknown, prefix = ''): Error {
  return new Error(
    prefix
      ? boundedErrorMessage(prefix, error)
      : boundedErrorDetail(error),
  );
}

export function boundedErrorText(value: string): string {
  return boundedErrorParts([value]);
}

function boundedErrorParts(parts: readonly string[]): string {
  const markerBytes = Buffer.byteLength(ERROR_TRUNCATION_MARKER);
  const contentBudget = ERROR_DETAIL_BUDGET_BYTES - markerBytes;
  let output = '';
  let outputBytes = 0;

  for (const part of parts) {
    for (const character of part) {
      const characterBytes = Buffer.byteLength(character);
      if (outputBytes + characterBytes > contentBudget) {
        return output + ERROR_TRUNCATION_MARKER;
      }
      output += character;
      outputBytes += characterBytes;
    }
  }

  return output;
}
