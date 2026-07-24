import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

export interface UnityReadClient {
  readTool(
    name: string,
    args: Record<string, unknown>,
  ): Promise<CallToolResult>;
}

export interface CompanionResourcePayload {
  uri: string;
  payload: Record<string, unknown>;
}

const LOG_SEVERITIES = new Set(['all', 'log', 'warning', 'error']);
const TEST_MODES = new Set(['all', 'editor', 'playmode']);
const NODE_NAME_MAX_LENGTH = 256;
const HIERARCHY_PATH_MAX_LENGTH = 1024;
const SCENE_NAME_MAX_LENGTH = 256;
const SCENE_PATH_MAX_LENGTH = 1024;
const INSTANCE_ID_MAX_LENGTH = 128;
const COMPONENT_MAX_COUNT = 32;
const COMPONENT_SCAN_LIMIT = 128;
const COMPONENT_NAME_MAX_LENGTH = 128;
// Keep hierarchy resources comfortably below common MCP host payload limits.
// The content allowance reserves fixed envelope/metadata space plus worst-case
// post-projection omission markers for every returned node.
const HIERARCHY_PAYLOAD_BUDGET_BYTES = 512 * 1024;
const HIERARCHY_ENVELOPE_RESERVE_BYTES = 16 * 1024;
const HIERARCHY_DYNAMIC_MARKER_RESERVE_PER_NODE = 128;

export class CompanionResourceService {
  constructor(private readonly client: UnityReadClient) {}

  async read(uri: string): Promise<CompanionResourcePayload> {
    const parsed = parseResourceUri(uri);
    switch (parsed.hostname) {
      case 'logs':
        return this.call(uri, 'get_console_logs', {
          severity: parseEnum(
            parsed.searchParams.get('severity') ?? 'all',
            LOG_SEVERITIES,
            'severity',
          ),
          limit: parseBoundedInteger(
            parsed.searchParams.get('limit'),
            100,
            1,
            1000,
            'limit',
          ),
        });
      case 'scenes-hierarchy': {
        const maxNodes = parseBoundedInteger(
          parsed.searchParams.get('max_nodes'),
          500,
          1,
          2000,
          'max_nodes',
        );
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
          include_indirect: parseBoolean(
            parsed.searchParams.get('include_indirect'),
            true,
            'include_indirect',
          ),
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

  private async call(
    uri: string,
    command: string,
    args: Record<string, unknown>,
  ): Promise<CompanionResourcePayload> {
    let result: CallToolResult;
    try {
      result = await this.client.readTool(command, args);
    } catch (error) {
      throw new Error(`${command} failed: ${errorMessage(error)}`);
    }
    return {
      uri,
      payload: decodeToolPayload(command, result),
    };
  }
}

export function decodeToolPayload(
  command: string,
  result: CallToolResult,
): Record<string, unknown> {
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
    const parsed: unknown = JSON.parse(text);
    if (!isRecord(parsed)) {
      throw new Error('payload is not a JSON object');
    }
    return parsed;
  } catch (error) {
    throw new Error(`${command} returned malformed JSON: ${errorMessage(error)}`);
  }
}

function parseResourceUri(uri: string): URL {
  let parsed: URL;
  try {
    parsed = new URL(uri);
  } catch {
    throw new Error(`Invalid companion resource URI: ${uri}`);
  }
  if (parsed.protocol !== 'unity:') {
    throw new Error(`Unknown companion resource: ${uri}`);
  }
  return parsed;
}

function parseBoundedInteger(
  value: string | null,
  fallback: number,
  minimum: number,
  maximum: number,
  name: string,
): number {
  if (value === null || value === '') return fallback;
  if (!/^-?[0-9]+$/.test(value)) {
    throw new Error(`${name} must be an integer.`);
  }
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed)) {
    return value.startsWith('-') ? minimum : maximum;
  }
  return Math.min(maximum, Math.max(minimum, parsed));
}

function parseBoolean(
  value: string | null,
  fallback: boolean,
  name: string,
): boolean {
  if (value === null || value === '') return fallback;
  if (value === 'true') return true;
  if (value === 'false') return false;
  throw new Error(`${name} must be true or false.`);
}

function parseEnum(
  value: string,
  allowed: ReadonlySet<string>,
  name: string,
): string {
  if (!allowed.has(value)) {
    throw new Error(`${name} must be one of: ${[...allowed].join(', ')}.`);
  }
  return value;
}

function firstText(result: CallToolResult): string | undefined {
  const item = result.content.find(
    (content): content is Extract<(typeof result.content)[number], { type: 'text' }> =>
      content.type === 'text',
  );
  return item?.text;
}

function truncateHierarchy(
  hierarchy: Record<string, unknown>,
  maxNodes: number,
): Record<string, unknown> {
  const sourceRoots = Array.isArray(hierarchy.roots) ? hierarchy.roots : [];
  const traversalBudget = Math.min(
    10_000,
    Math.max(maxNodes * 4, maxNodes + 1024),
  );
  const roots: HierarchyOutputNode[] = [];
  const frames: HierarchyTraversalFrame[] = [];
  const omissionOwners = new Set<HierarchyOutputNode>();
  const projectedContentBudget = Math.max(
    0,
    HIERARCHY_PAYLOAD_BUDGET_BYTES -
      HIERARCHY_ENVELOPE_RESERVE_BYTES -
      maxNodes * HIERARCHY_DYNAMIC_MARKER_RESERVE_PER_NODE,
  );
  let rootIndex = 0;
  let visitedNodes = 0;
  let returnedNodes = 0;
  let rootsTruncated = false;
  let projectedContentBytes = 0;
  let payloadBudgetReached = false;
  let omittedAtBudgetNodes = 0;
  let omittedAtBudgetComponents = 0;

  while (visitedNodes < traversalBudget) {
    let source: unknown;
    let parentOutput: HierarchyOutputNode | undefined;
    let inheritedOmissionOwner: HierarchyOutputNode | undefined;
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
      if (rootIndex >= sourceRoots.length) break;
      source = sourceRoots[rootIndex++];
      isRoot = true;
    }

    visitedNodes++;
    if (!isRecord(source)) continue;

    let output: HierarchyOutputNode | undefined;
    let omissionOwner: HierarchyOutputNode | undefined;
    let countedBudgetOmission = false;
    const outputEligible =
      returnedNodes < maxNodes && (isRoot || parentOutput !== undefined);
    if (outputEligible && !payloadBudgetReached) {
      const destination = parentOutput?.children ?? roots;
      const separatorBytes = destination.length > 0 ? 1 : 0;
      const remainingBytes =
        projectedContentBudget - projectedContentBytes - separatorBytes;
      const candidate = projectHierarchyNode(source);
      const fitted = fitProjectedNodeToBudget(candidate, remainingBytes);
      if (fitted.output) {
        output = fitted.output;
        returnedNodes++;
        projectedContentBytes += separatorBytes + fitted.serializedBytes;
        omittedAtBudgetComponents += fitted.omittedAtBudgetComponents;
        destination.push(output);
        if (fitted.payloadBudgetReached) {
          payloadBudgetReached = true;
        }
      } else {
        payloadBudgetReached = true;
        omittedAtBudgetNodes++;
        omittedAtBudgetComponents += sourceComponentCount(source);
        countedBudgetOmission = true;
      }
    }
    if (!output) {
      if (
        payloadBudgetReached &&
        !countedBudgetOmission &&
        isRecord(source)
      ) {
        omittedAtBudgetNodes++;
        omittedAtBudgetComponents += sourceComponentCount(source);
      }
      omissionOwner = parentOutput ?? inheritedOmissionOwner;
      if (omissionOwner) {
        omissionOwner.childrenTruncated = true;
        omissionOwner.omittedDescendants =
          (omissionOwner.omittedDescendants ?? 0) + 1;
        omissionOwners.add(omissionOwner);
      } else {
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

  const hasRemaining =
    rootIndex < sourceRoots.length ||
    frames.some((frame) => frame.nextChild < frame.children.length);
  const totalNodesKnown = !hasRemaining;

  if (totalNodesKnown) {
    for (const owner of omissionOwners) {
      owner.omittedDescendantsKnown = true;
    }
  } else {
    if (rootIndex < sourceRoots.length) rootsTruncated = true;
    for (const owner of omissionOwners) {
      owner.omittedDescendantsKnown = false;
    }
    for (const frame of frames) {
      if (frame.nextChild >= frame.children.length) continue;
      const owner = frame.output ?? frame.omissionOwner;
      if (owner) {
        owner.childrenTruncated = true;
        owner.omittedDescendantsKnown = false;
      } else {
        rootsTruncated = true;
      }
    }
  }

  const truncation: Record<string, unknown> = totalNodesKnown
    ? {
        truncated: returnedNodes < visitedNodes || payloadBudgetReached,
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

  Object.assign(truncation, {
    payloadBudgetReached,
    payloadBudgetBytes: HIERARCHY_PAYLOAD_BUDGET_BYTES,
    projectedBytes: 0,
    omittedAtBudgetNodes,
    omittedAtBudgetComponents,
  });

  const result: Record<string, unknown> = {
    ...projectHierarchyMetadata(hierarchy),
    roots,
    truncation,
  };
  stabilizeProjectedByteCount(result, truncation);
  return result;
}

interface HierarchyOutputNode extends Record<string, unknown> {
  children: HierarchyOutputNode[];
  childrenTruncated: boolean;
  omittedDescendants?: number;
  omittedDescendantsKnown?: boolean;
}

interface HierarchyTraversalFrame {
  children: unknown[];
  nextChild: number;
  output?: HierarchyOutputNode;
  omissionOwner?: HierarchyOutputNode;
}

interface FittedHierarchyNode {
  output?: HierarchyOutputNode;
  serializedBytes: number;
  payloadBudgetReached: boolean;
  omittedAtBudgetComponents: number;
}

function fitProjectedNodeToBudget(
  candidate: HierarchyOutputNode,
  maxBytes: number,
): FittedHierarchyNode {
  let serializedBytes = jsonBytes(candidate);
  if (serializedBytes <= maxBytes) {
    return {
      output: candidate,
      serializedBytes,
      payloadBudgetReached: false,
      omittedAtBudgetComponents: 0,
    };
  }

  const components = Array.isArray(candidate.components)
    ? candidate.components as string[]
    : undefined;
  if (!components || components.length === 0) {
    return {
      serializedBytes,
      payloadBudgetReached: true,
      omittedAtBudgetComponents: 0,
    };
  }

  const initialReturnedCount = components.length;
  while (components.length > 0) {
    components.pop();
    markComponentsOmittedAtBudget(
      candidate,
      initialReturnedCount - components.length,
      initialReturnedCount,
    );
    serializedBytes = jsonBytes(candidate);
    if (serializedBytes <= maxBytes) {
      return {
        output: candidate,
        serializedBytes,
        payloadBudgetReached: true,
        omittedAtBudgetComponents:
          initialReturnedCount - components.length,
      };
    }
  }

  return {
    serializedBytes,
    payloadBudgetReached: true,
    omittedAtBudgetComponents: initialReturnedCount,
  };
}

function markComponentsOmittedAtBudget(
  node: HierarchyOutputNode,
  omittedAtBudgetCount: number,
  initialReturnedCount: number,
): void {
  const projection = isRecord(node.projection)
    ? node.projection as ProjectionNotice
    : {};
  node.projection = projection;
  const existing = projection.components;
  const returnedCount = initialReturnedCount - omittedAtBudgetCount;
  projection.components = {
    sourceCount: existing?.sourceCount ?? initialReturnedCount,
    scannedCount: existing?.scannedCount ?? initialReturnedCount,
    returnedCount,
    omittedCount:
      (existing?.sourceCount ?? initialReturnedCount) - returnedCount,
    invalidScanned: existing?.invalidScanned ?? 0,
    namesTruncated: existing?.namesTruncated ?? 0,
    scanTruncated: existing?.scanTruncated ?? false,
    payloadBudgetReached: true,
    omittedAtBudgetCount,
  };
}

function sourceComponentCount(source: Record<string, unknown>): number {
  return Array.isArray(source.components) ? source.components.length : 0;
}

function stabilizeProjectedByteCount(
  result: Record<string, unknown>,
  truncation: Record<string, unknown>,
): void {
  for (let attempt = 0; attempt < 4; attempt++) {
    const projectedBytes = jsonBytes(result);
    if (truncation.projectedBytes === projectedBytes) return;
    truncation.projectedBytes = projectedBytes;
  }
}

function jsonBytes(value: unknown): number {
  return Buffer.byteLength(JSON.stringify(value));
}

function projectHierarchyNode(
  source: Record<string, unknown>,
): HierarchyOutputNode {
  const output: HierarchyOutputNode = {
    children: [],
    childrenTruncated: source.childrenTruncated === true,
  };
  const projection: ProjectionNotice = {};
  copyBoundedString(
    source,
    output,
    projection,
    'name',
    NODE_NAME_MAX_LENGTH,
  );
  copyBoundedString(
    source,
    output,
    projection,
    'hierarchyPath',
    HIERARCHY_PATH_MAX_LENGTH,
  );
  copyBoundedInstanceId(source, output, projection);
  copyBoolean(source, output, projection, 'activeSelf');
  copyBoundedComponents(source, output, projection);
  if (hasProjectionNotice(projection)) {
    output.projection = projection;
  }
  return output;
}

function projectHierarchyMetadata(
  hierarchy: Record<string, unknown>,
): Record<string, unknown> {
  const metadata: Record<string, unknown> = {};
  const projection: ProjectionNotice = {};
  copyBoundedString(
    hierarchy,
    metadata,
    projection,
    'sceneName',
    SCENE_NAME_MAX_LENGTH,
  );
  copyBoundedString(
    hierarchy,
    metadata,
    projection,
    'scenePath',
    SCENE_PATH_MAX_LENGTH,
  );
  copyBoolean(hierarchy, metadata, projection, 'isDirty');
  copyBoolean(hierarchy, metadata, projection, 'isActive');
  if (hasProjectionNotice(projection)) {
    metadata.metadataProjection = projection;
  }
  return metadata;
}

interface ProjectionNotice {
  truncatedStringCount?: number;
  truncatedStrings?: Record<
    string,
    { originalLength: number; returnedLength: number }
  >;
  omittedKnownFieldCount?: number;
  omittedKnownFields?: string[];
  components?: {
    sourceCount: number;
    scannedCount: number;
    returnedCount: number;
    omittedCount: number;
    invalidScanned: number;
    namesTruncated: number;
    scanTruncated: boolean;
    payloadBudgetReached?: boolean;
    omittedAtBudgetCount?: number;
  };
}

function copyBoundedString(
  source: Record<string, unknown>,
  output: Record<string, unknown>,
  projection: ProjectionNotice,
  field: string,
  maxLength: number,
): void {
  if (!(field in source)) return;
  const value = source[field];
  if (typeof value !== 'string') {
    markOmittedField(projection, field);
    return;
  }
  output[field] = boundedString(value, maxLength, projection, field);
}

function copyBoundedInstanceId(
  source: Record<string, unknown>,
  output: Record<string, unknown>,
  projection: ProjectionNotice,
): void {
  if (!('instanceId' in source)) return;
  const value = source.instanceId;
  if (typeof value === 'number' && Number.isSafeInteger(value)) {
    output.instanceId = value;
    return;
  }
  if (typeof value === 'string') {
    output.instanceId = boundedString(
      value,
      INSTANCE_ID_MAX_LENGTH,
      projection,
      'instanceId',
    );
    return;
  }
  markOmittedField(projection, 'instanceId');
}

function copyBoolean(
  source: Record<string, unknown>,
  output: Record<string, unknown>,
  projection: ProjectionNotice,
  field: string,
): void {
  if (!(field in source)) return;
  const value = source[field];
  if (typeof value === 'boolean') {
    output[field] = value;
  } else {
    markOmittedField(projection, field);
  }
}

function copyBoundedComponents(
  source: Record<string, unknown>,
  output: Record<string, unknown>,
  projection: ProjectionNotice,
): void {
  if (!('components' in source)) return;
  if (!Array.isArray(source.components)) {
    markOmittedField(projection, 'components');
    return;
  }

  const sourceComponents = source.components;
  const components: string[] = [];
  let scannedCount = 0;
  let invalidScanned = 0;
  let namesTruncated = 0;
  const scanCount = Math.min(sourceComponents.length, COMPONENT_SCAN_LIMIT);
  while (
    scannedCount < scanCount &&
    components.length < COMPONENT_MAX_COUNT
  ) {
    const candidate = sourceComponents[scannedCount++];
    const name =
      typeof candidate === 'string'
        ? candidate
        : isRecord(candidate) && typeof candidate.name === 'string'
          ? candidate.name
          : undefined;
    if (name === undefined) {
      invalidScanned++;
      continue;
    }
    if (name.length > COMPONENT_NAME_MAX_LENGTH) namesTruncated++;
    components.push(name.slice(0, COMPONENT_NAME_MAX_LENGTH));
  }
  output.components = components;

  const omittedCount = sourceComponents.length - components.length;
  if (omittedCount > 0 || namesTruncated > 0 || invalidScanned > 0) {
    projection.components = {
      sourceCount: sourceComponents.length,
      scannedCount,
      returnedCount: components.length,
      omittedCount,
      invalidScanned,
      namesTruncated,
      scanTruncated: scannedCount < sourceComponents.length,
    };
  }
}

function boundedString(
  value: string,
  maxLength: number,
  projection: ProjectionNotice,
  field: string,
): string {
  if (value.length <= maxLength) return value;
  projection.truncatedStringCount =
    (projection.truncatedStringCount ?? 0) + 1;
  projection.truncatedStrings ??= {};
  projection.truncatedStrings[field] = {
    originalLength: value.length,
    returnedLength: maxLength,
  };
  return value.slice(0, maxLength);
}

function markOmittedField(
  projection: ProjectionNotice,
  field: string,
): void {
  projection.omittedKnownFieldCount =
    (projection.omittedKnownFieldCount ?? 0) + 1;
  projection.omittedKnownFields ??= [];
  projection.omittedKnownFields.push(field);
}

function hasProjectionNotice(projection: ProjectionNotice): boolean {
  return (
    projection.truncatedStrings !== undefined ||
    projection.omittedKnownFields !== undefined ||
    projection.components !== undefined
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
