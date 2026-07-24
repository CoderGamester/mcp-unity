import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { RESOURCE_MIME_TYPE } from '@modelcontextprotocol/ext-apps/server';
export const DASHBOARD_URI = 'ui://unity-dashboard';
export function readDashboardHtml() {
    const moduleDirectory = path.dirname(fileURLToPath(import.meta.url));
    const candidates = [
        path.join(moduleDirectory, '..', 'ui', 'unity-dashboard.html'),
        path.join(moduleDirectory, '..', '..', 'src', 'ui', 'unity-dashboard.html'),
    ];
    const dashboardPath = candidates.find((candidate) => fs.existsSync(candidate));
    if (!dashboardPath) {
        throw new Error(`Unity dashboard HTML is missing. Checked: ${candidates.join(', ')}`);
    }
    return {
        text: fs.readFileSync(dashboardPath, 'utf8'),
        mimeType: RESOURCE_MIME_TYPE,
    };
}
