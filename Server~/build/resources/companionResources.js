const LOG_SEVERITIES = new Set(['all', 'log', 'warning', 'error']);
const TEST_MODES = new Set(['all', 'editor', 'playmode']);
export class CompanionResourceService {
    client;
    constructor(client) {
        this.client = client;
    }
    async read(uri) {
        const parsed = parseResourceUri(uri);
        switch (parsed.hostname) {
            case 'logs':
                return this.call(uri, 'get_console_logs', {
                    severity: parseEnum(parsed.searchParams.get('severity') ?? 'all', LOG_SEVERITIES, 'severity'),
                    limit: parseBoundedInteger(parsed.searchParams.get('limit'), 100, 1, 1000, 'limit'),
                });
            case 'scenes-hierarchy': {
                const maxNodes = parseBoundedInteger(parsed.searchParams.get('max_nodes'), 500, 1, 2000, 'max_nodes');
                const path = parsed.searchParams.get('path');
                const args = path ? { path } : {};
                const result = await this.call(uri, 'get_scene_hierarchy', args);
                return {
                    uri,
                    payload: truncateHierarchy(result.payload, maxNodes),
                };
            }
            case 'gameobject': {
                const target = decodeURIComponent(parsed.pathname.replace(/^\/+/, ''));
                if (!target) {
                    throw new Error('unity://gameobject/{target} requires a target.');
                }
                return this.call(uri, 'inspect_gameobject', {
                    target,
                    max_depth: 2,
                    max_nodes: 200,
                    include_components: true,
                    include_properties: true,
                    max_properties_per_component: 100,
                });
            }
            case 'packages':
                return this.call(uri, 'package_list', {
                    scope: 'installed',
                    include_indirect: parseBoolean(parsed.searchParams.get('include_indirect'), true, 'include_indirect'),
                });
            case 'tests': {
                const mode = decodeURIComponent(parsed.pathname.replace(/^\/+/, ''));
                parseEnum(mode, TEST_MODES, 'mode');
                return this.call(uri, 'list_tests', { mode });
            }
            default:
                throw new Error(`Unknown companion resource: ${uri}`);
        }
    }
    async call(uri, command, args) {
        let result;
        try {
            result = await this.client.readTool(command, args);
        }
        catch (error) {
            throw new Error(`${command} failed: ${errorMessage(error)}`);
        }
        return {
            uri,
            payload: decodeToolPayload(command, result),
        };
    }
}
export function decodeToolPayload(command, result) {
    if (result.isError) {
        const detail = firstText(result) ?? 'Unity command returned an error.';
        throw new Error(`${command} failed: ${detail}`);
    }
    if (isRecord(result.structuredContent)) {
        return result.structuredContent;
    }
    const text = firstText(result);
    if (text === undefined) {
        throw new Error(`${command} returned no JSON payload.`);
    }
    try {
        const parsed = JSON.parse(text);
        if (!isRecord(parsed)) {
            throw new Error('payload is not a JSON object');
        }
        return parsed;
    }
    catch (error) {
        throw new Error(`${command} returned malformed JSON: ${errorMessage(error)}`);
    }
}
function parseResourceUri(uri) {
    let parsed;
    try {
        parsed = new URL(uri);
    }
    catch {
        throw new Error(`Invalid companion resource URI: ${uri}`);
    }
    if (parsed.protocol !== 'unity:') {
        throw new Error(`Unknown companion resource: ${uri}`);
    }
    return parsed;
}
function parseBoundedInteger(value, fallback, minimum, maximum, name) {
    if (value === null || value === '')
        return fallback;
    if (!/^-?[0-9]+$/.test(value)) {
        throw new Error(`${name} must be an integer.`);
    }
    const parsed = Number(value);
    if (!Number.isSafeInteger(parsed)) {
        return value.startsWith('-') ? minimum : maximum;
    }
    return Math.min(maximum, Math.max(minimum, parsed));
}
function parseBoolean(value, fallback, name) {
    if (value === null || value === '')
        return fallback;
    if (value === 'true')
        return true;
    if (value === 'false')
        return false;
    throw new Error(`${name} must be true or false.`);
}
function parseEnum(value, allowed, name) {
    if (!allowed.has(value)) {
        throw new Error(`${name} must be one of: ${[...allowed].join(', ')}.`);
    }
    return value;
}
function firstText(result) {
    const item = result.content.find((content) => content.type === 'text');
    return item?.text;
}
function truncateHierarchy(hierarchy, maxNodes) {
    const sourceRoots = Array.isArray(hierarchy.roots) ? hierarchy.roots : [];
    const traversalBudget = Math.min(10_000, Math.max(maxNodes * 4, maxNodes + 1024));
    const roots = [];
    const frames = [];
    const omissionOwners = new Set();
    let rootIndex = 0;
    let visitedNodes = 0;
    let returnedNodes = 0;
    let rootsTruncated = false;
    while (visitedNodes < traversalBudget) {
        let source;
        let parentOutput;
        let inheritedOmissionOwner;
        let isRoot = false;
        while (frames.length > 0) {
            const frame = frames[frames.length - 1];
            if (frame.nextChild < frame.children.length) {
                source = frame.children[frame.nextChild++];
                parentOutput = frame.output;
                inheritedOmissionOwner = frame.omissionOwner;
                break;
            }
            frames.pop();
        }
        if (source === undefined) {
            if (rootIndex >= sourceRoots.length)
                break;
            source = sourceRoots[rootIndex++];
            isRoot = true;
        }
        visitedNodes++;
        if (!isRecord(source))
            continue;
        let output;
        let omissionOwner;
        if (returnedNodes < maxNodes && (isRoot || parentOutput !== undefined)) {
            output = projectHierarchyNode(source);
            returnedNodes++;
            if (parentOutput) {
                parentOutput.children.push(output);
            }
            else {
                roots.push(output);
            }
        }
        else {
            omissionOwner = parentOutput ?? inheritedOmissionOwner;
            if (omissionOwner) {
                omissionOwner.childrenTruncated = true;
                omissionOwner.omittedDescendants =
                    (omissionOwner.omittedDescendants ?? 0) + 1;
                omissionOwners.add(omissionOwner);
            }
            else {
                rootsTruncated = true;
            }
        }
        const children = Array.isArray(source.children) ? source.children : [];
        if (children.length > 0) {
            frames.push({
                children,
                nextChild: 0,
                output,
                omissionOwner: output ? undefined : omissionOwner,
            });
        }
    }
    const hasRemaining = rootIndex < sourceRoots.length ||
        frames.some((frame) => frame.nextChild < frame.children.length);
    const totalNodesKnown = !hasRemaining;
    if (totalNodesKnown) {
        for (const owner of omissionOwners) {
            owner.omittedDescendantsKnown = true;
        }
    }
    else {
        if (rootIndex < sourceRoots.length)
            rootsTruncated = true;
        for (const owner of omissionOwners) {
            owner.omittedDescendantsKnown = false;
        }
        for (const frame of frames) {
            if (frame.nextChild >= frame.children.length)
                continue;
            const owner = frame.output ?? frame.omissionOwner;
            if (owner) {
                owner.childrenTruncated = true;
                owner.omittedDescendantsKnown = false;
            }
            else {
                rootsTruncated = true;
            }
        }
    }
    const truncation = totalNodesKnown
        ? {
            truncated: returnedNodes < visitedNodes,
            maxNodes,
            traversalBudget,
            visitedNodes,
            returnedNodes,
            totalNodesKnown: true,
            totalNodes: visitedNodes,
            omittedNodes: visitedNodes - returnedNodes,
            rootsTruncated,
        }
        : {
            truncated: true,
            maxNodes,
            traversalBudget,
            visitedNodes,
            returnedNodes,
            totalNodesKnown: false,
            totalNodesAtLeast: visitedNodes + 1,
            omittedNodesAtLeast: visitedNodes + 1 - returnedNodes,
            rootsTruncated,
        };
    return {
        ...projectHierarchyMetadata(hierarchy),
        roots,
        truncation,
    };
}
function projectHierarchyNode(source) {
    const output = {
        children: [],
        childrenTruncated: source.childrenTruncated === true,
    };
    for (const field of [
        'name',
        'instanceId',
        'hierarchyPath',
        'activeSelf',
        'components',
    ]) {
        if (field in source)
            output[field] = source[field];
    }
    return output;
}
function projectHierarchyMetadata(hierarchy) {
    const metadata = {};
    for (const field of ['sceneName', 'scenePath', 'isDirty', 'isActive']) {
        if (field in hierarchy)
            metadata[field] = hierarchy[field];
    }
    return metadata;
}
function isRecord(value) {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}
function errorMessage(error) {
    return error instanceof Error ? error.message : String(error);
}
