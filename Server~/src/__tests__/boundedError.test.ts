import {
  ERROR_DETAIL_BUDGET_BYTES,
  boundedErrorMessage,
} from '../utils/boundedError.js';

describe('bounded companion error details', () => {
  test('uses a UTF-8 byte ceiling without splitting surrogate pairs', () => {
    const message = boundedErrorMessage(
      'transport failed: ',
      `${'🙂'.repeat(16 * 1024)}-secret-tail`,
    );

    expect(Buffer.byteLength(message)).toBeLessThanOrEqual(
      ERROR_DETAIL_BUDGET_BYTES,
    );
    expect(message).toContain('[truncated]');
    expect(message).not.toContain('\uFFFD');
    expect(message).not.toContain('secret-tail');
  });
});
