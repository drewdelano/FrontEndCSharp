import { installManagedDebuggerHooks } from "./managed-debugger-hooks.js";

const breakpointLocations = new Map();

let currentStop = null;
let enabled = false;
let lastStop = null;
let pauseRequested = false;
let stepPlan = null;
let stopSequence = 0;
let exceptionFilters = Object.freeze({ thrown: false, unhandled: false });
let exceptionStates = new WeakMap();
let exceptionStatesByValue = new WeakMap();
let updateState = Object.freeze({ baselineBuildId: null, generation: 0, hash: null });
let configuredBuildId = null;
const configurationWaiters = new Set();

function debuggerIsEnabled() {
    return pauseRequested
        || breakpointLocations.size > 0
        || stepPlan !== null
        || exceptionFilters.thrown
        || exceptionFilters.unhandled;
}

function locationKey(moduleId, methodToken, blockIndex, instructionIndex) {
    return `${moduleId}:${methodToken}:${blockIndex}:${instructionIndex}`;
}

function validateBreakpoint(breakpoint) {
    if (breakpoint === null || typeof breakpoint !== "object") {
        throw new TypeError("A managed breakpoint must be an object.");
    }
    if (!Number.isInteger(breakpoint.id) || breakpoint.id < 0) {
        throw new TypeError("A managed breakpoint requires a non-negative integer ID.");
    }
    if (typeof breakpoint.moduleId !== "string" || breakpoint.moduleId.length === 0) {
        throw new TypeError("A managed breakpoint requires a non-empty module ID.");
    }
    if (!Number.isInteger(breakpoint.methodToken) || breakpoint.methodToken <= 0) {
        throw new TypeError("A managed breakpoint requires a positive integer method token.");
    }
    if (!Number.isInteger(breakpoint.blockIndex) || breakpoint.blockIndex < 0) {
        throw new TypeError("A managed breakpoint requires a non-negative integer block index.");
    }
    if (!Number.isInteger(breakpoint.instructionIndex) || breakpoint.instructionIndex < 0) {
        throw new TypeError("A managed breakpoint requires a non-negative integer instruction index.");
    }
}

function readLocation(opts) {
    const moduleId = opts?.methodInfo?.debugModuleId;
    const methodToken = opts?.methodInfo?.debugMethodToken;
    const blockIndex = opts?.currentBlockIndex;
    const instructionIndex = opts?.instructionIndex;

    if (typeof moduleId !== "string"
        || !Number.isInteger(methodToken)
        || !Number.isInteger(blockIndex)
        || !Number.isInteger(instructionIndex)) {
        throw new Error(
            "Managed debugging requires a module ID, method token, block index, and instruction index.");
    }

    return { moduleId, methodToken, blockIndex, instructionIndex };
}

function setBreakpoints(breakpoints) {
    if (!Array.isArray(breakpoints)) {
        throw new TypeError("Managed breakpoints must be supplied as an array.");
    }

    const nextLocations = new Map();
    for (const breakpoint of breakpoints) {
        validateBreakpoint(breakpoint);
        const key = locationKey(
            breakpoint.moduleId,
            breakpoint.methodToken,
            breakpoint.blockIndex,
            breakpoint.instructionIndex);
        const ids = nextLocations.get(key) ?? [];
        ids.push(breakpoint.id);
        nextLocations.set(key, ids);
    }

    ensureRuntimeHooksInstalled();
    breakpointLocations.clear();
    for (const [key, ids] of nextLocations) {
        breakpointLocations.set(key, Object.freeze(ids));
    }
    enabled = debuggerIsEnabled();
}

function requestPause() {
    ensureRuntimeHooksInstalled();
    pauseRequested = true;
    enabled = true;
}

function requestStep(kind, baselineDepth, locations) {
    if (!new Set(["in", "over", "out"]).has(kind)) {
        throw new TypeError("A managed step kind must be 'in', 'over', or 'out'.");
    }
    if (!Number.isInteger(baselineDepth) || baselineDepth <= 0) {
        throw new TypeError("A managed step requires a positive baseline depth.");
    }
    if (!Array.isArray(locations)) {
        throw new TypeError("Managed step locations must be supplied as an array.");
    }
    const keys = new Set();
    for (const location of locations) {
        validateBreakpoint({ id: 0, ...location });
        keys.add(locationKey(
            location.moduleId,
            location.methodToken,
            location.blockIndex,
            location.instructionIndex));
    }
    ensureRuntimeHooksInstalled();
    stepPlan = Object.freeze({ kind, baselineDepth, locations: keys });
    enabled = true;
}

function setExceptionBreakpoints(filters) {
    if (!Array.isArray(filters)
        || filters.some(filter => filter !== "thrown" && filter !== "unhandled")) {
        throw new TypeError("Managed exception breakpoints must contain only 'thrown' or 'unhandled'.");
    }
    ensureRuntimeHooksInstalled();
    exceptionFilters = Object.freeze({
        thrown: filters.includes("thrown"),
        unhandled: filters.includes("unhandled")
    });
    enabled = debuggerIsEnabled();
}

function disable() {
    breakpointLocations.clear();
    currentStop = null;
    enabled = false;
    lastStop = null;
    pauseRequested = false;
    stepPlan = null;
    exceptionFilters = Object.freeze({ thrown: false, unhandled: false });
    exceptionStates = new WeakMap();
    exceptionStatesByValue = new WeakMap();
    stopSequence = 0;
}

function initializeUpdates(baselineBuildId, hash) {
    if (typeof baselineBuildId !== "string" || baselineBuildId.length === 0) {
        throw new TypeError("A non-empty baseline build ID is required.");
    }
    if (typeof hash !== "string" || hash.length === 0) {
        throw new TypeError("A non-empty baseline content hash is required.");
    }
    if (updateState.baselineBuildId !== null
        && (updateState.baselineBuildId !== baselineBuildId || updateState.hash !== hash)) {
        throw new Error("The runtime update baseline has already been initialized for another build.");
    }
    updateState = Object.freeze({ baselineBuildId, generation: 0, hash });
    return updateState;
}

async function configure(configuration) {
    if (configuration === null
        || typeof configuration !== "object"
        || typeof configuration.buildId !== "string"
        || configuration.buildId.length === 0) {
        throw new TypeError("A debugger configuration with a non-empty build ID is required.");
    }
    const { buildId } = await import("./debug-build.js");
    if (buildId !== configuration.buildId) {
        throw new Error("Generated JavaScript does not match the selected debug manifest.");
    }
    initializeUpdates(buildId, buildId);
    setBreakpoints(configuration.breakpoints ?? []);
    setExceptionBreakpoints(configuration.exceptionFilters ?? []);
    configuredBuildId = buildId;
    for (const waiter of configurationWaiters) {
        clearTimeout(waiter.timer);
        configurationWaiters.delete(waiter);
        if (waiter.buildId === buildId) {
            waiter.resolve();
        } else {
            waiter.reject(new Error(
                `The debugger configured build '${buildId}', not the waiting build '${waiter.buildId}'.`));
        }
    }
    return Object.freeze({ buildId });
}

export function waitForDebuggerConfiguration(buildId, timeoutMilliseconds) {
    if (typeof buildId !== "string" || buildId.length === 0) {
        throw new TypeError("A non-empty debugger build ID is required.");
    }
    if (!Number.isInteger(timeoutMilliseconds)
        || timeoutMilliseconds <= 0
        || timeoutMilliseconds > 300_000) {
        throw new TypeError("The debugger rendezvous timeout must be an integer from 1 through 300000 milliseconds.");
    }
    if (configuredBuildId === buildId) {
        return Promise.resolve();
    }
    if (configuredBuildId !== null) {
        return Promise.reject(new Error(
            `The debugger configured build '${configuredBuildId}', not the waiting build '${buildId}'.`));
    }

    return new Promise((resolve, reject) => {
        const waiter = { buildId, resolve, reject, timer: null };
        waiter.timer = setTimeout(() => {
            configurationWaiters.delete(waiter);
            const error = new Error(
                `FrontEndCSharp debugger did not attach within ${timeoutMilliseconds} milliseconds; managed entry-point execution was canceled.`);
            console.error(error.message);
            reject(error);
        }, timeoutMilliseconds);
        configurationWaiters.add(waiter);
    });
}

function applyUpdate(update) {
    if (updateState.baselineBuildId === null) {
        throw new Error("The runtime update baseline has not been initialized.");
    }
    if (update === null || typeof update !== "object") {
        throw new TypeError("An update package is required.");
    }
    if (update.baselineBuildId !== updateState.baselineBuildId) {
        throw new Error("The update package targets a different baseline build.");
    }
    if (update.expectedGeneration !== updateState.generation
        || update.nextGeneration !== updateState.generation + 1) {
        throw new Error(
            `The update package is stale or out of order. Runtime generation is ${updateState.generation}.`);
    }
    if (typeof update.hash !== "string" || update.hash.length === 0) {
        throw new TypeError("The update package requires a non-empty content hash.");
    }
    if (!Array.isArray(update.methods) || update.methods.length === 0) {
        throw new TypeError("The update package must replace at least one method.");
    }

    const activeMethods = new Set((currentStop?.frame?.ctx?.debugFrames ?? [])
        .map(frame => `${frame?.methodInfo?.debugModuleId}:${frame?.methodInfo?.debugMethodToken}`));
    const names = new Set();
    const replacements = [];
    for (const method of update.methods) {
        if (method === null || typeof method !== "object"
            || typeof method.name !== "string" || method.name.length === 0
            || typeof method.moduleId !== "string" || method.moduleId.length === 0
            || !Number.isInteger(method.methodToken) || method.methodToken <= 0
            || typeof method.implementation !== "function") {
            throw new TypeError("Each replacement requires a name, module ID, method token, and function.");
        }
        if (names.has(method.name)) {
            throw new Error(`The update package replaces '${method.name}' more than once.`);
        }
        names.add(method.name);
        if (typeof globalThis.DotNet?.Methods?.[method.name] !== "function") {
            throw new Error(`The update package references unknown method '${method.name}'.`);
        }
        if (activeMethods.has(`${method.moduleId}:${method.methodToken}`)) {
            throw new Error(`Cannot replace active method '${method.name}'.`);
        }
        replacements.push(Object.freeze({ name: method.name, implementation: method.implementation }));
    }

    // Validation above is deliberately complete. These assignments cannot execute user code,
    // so no JavaScript task can observe a partially replaced method registry.
    for (const replacement of replacements) {
        globalThis.DotNet.Methods[replacement.name] = replacement.implementation;
    }
    stepPlan = null;
    updateState = Object.freeze({
        baselineBuildId: updateState.baselineBuildId,
        generation: update.nextGeneration,
        hash: update.hash
    });
    enabled = debuggerIsEnabled();
    return updateState;
}

function getExceptionState(opts, error) {
    if (error === null
        || typeof error !== "object"
        || typeof error.dotNetException?.t !== "string") {
        return null;
    }
    let state = exceptionStates.get(error)
        ?? exceptionStatesByValue.get(error.dotNetException);
    if (state !== undefined) {
        exceptionStates.set(error, state);
        return state;
    }
    const frames = [
        ...(opts.ctx?.debugFrames ?? [opts]).map(frame => snapshotFrame(frame)),
        ...(opts.ctx?.asyncParents ?? [])
    ];
    state = {
        exceptionType: error.dotNetException.t,
        frames: Object.freeze(frames),
        thrownReported: false,
        unhandledReported: false
    };
    exceptionStates.set(error, state);
    exceptionStatesByValue.set(error.dotNetException, state);
    return state;
}

function snapshotValue(value) {
    const type = value && typeof value === "object" && typeof value.t === "string"
        ? value.t
        : value === null ? "null" : typeof value;
    const raw = value && typeof value === "object" && "t" in value && "v" in value
        ? value.v
        : value;
    let text;
    if (raw === null) text = "null";
    else if (typeof raw === "string") text = JSON.stringify(raw);
    else if (typeof raw === "bigint") text = raw.toString();
    else if (typeof raw === "number" && Object.is(raw, -0)) text = "-0";
    else if (typeof raw === "object") text = `{${type}}`;
    else text = String(raw);
    const display = raw === null
        || typeof raw === "string"
        || typeof raw === "number"
        || typeof raw === "bigint"
        || typeof raw === "boolean"
        ? (typeof raw === "number" && Object.is(raw, -0) ? text : raw)
        : text;
    return Object.freeze({ type, value: text, display });
}

function snapshotFields(frame) {
    const receiver = frame.methodArgs?.[0];
    const referenced = receiver && typeof receiver === "object" && "value" in receiver
        ? receiver.value
        : receiver;
    const instance = referenced && typeof referenced === "object" && "v" in referenced
        ? referenced.v
        : referenced;
    if (instance === null || typeof instance !== "object") {
        return Object.freeze([]);
    }
    return Object.freeze(getFieldSnapshotEntries(instance).map(({ name, value }) => Object.freeze({
        name,
        ...snapshotValue(value)
    })));
}

function getFieldSnapshotEntries(instance) {
    const entries = Object.entries(instance).map(([storageKey, value]) => ({
        storageKey,
        value,
        definition: findFieldDefinition(storageKey)
    }));
    const nameCounts = new Map();
    for (const entry of entries) {
        const name = entry.definition?.name ?? entry.storageKey;
        nameCounts.set(name, (nameCounts.get(name) ?? 0) + 1);
    }
    return entries.map(entry => {
        const logicalName = entry.definition?.name ?? entry.storageKey;
        const name = nameCounts.get(logicalName) > 1
            ? `${entry.definition?.declaringType ?? "unknown"}.${logicalName}`
            : logicalName;
        return { name, value: entry.value };
    });
}

function findFieldDefinition(storageKey) {
    for (const type of Object.values(globalThis.DotNet?.Types ?? {})) {
        const definition = type?.instances?.[storageKey];
        if (definition !== undefined) return definition;
    }
    return undefined;
}

function snapshotFrame(frame, isLogical = false) {
    return Object.freeze({
        moduleId: frame.methodInfo.debugModuleId,
        methodToken: frame.methodInfo.debugMethodToken,
        blockIndex: frame.currentBlockIndex,
        instructionIndex: frame.instructionIndex,
        debugDepth: frame.ctx?.debugDepth ?? 1,
        arguments: Object.freeze(frame.methodArgs.map(snapshotValue)),
        locals: Object.freeze(frame.locals.map(local => snapshotValue(local.value))),
        fields: snapshotFields(frame),
        isLogical
    });
}

export function captureAsyncCausality(ctx) {
    const physicalParents = (ctx?.debugFrames ?? []).slice(1)
        .filter(frame => frame?.methodInfo?.debugModuleId && frame?.methodInfo?.debugMethodToken)
        .map(frame => snapshotFrame(frame, true));
    const inheritedParents = Array.isArray(ctx?.asyncParents) ? ctx.asyncParents : [];
    return Object.freeze([...physicalParents, ...inheritedParents]);
}

export function createAsyncContinuationContext(ctx) {
    return {
        ...ctx,
        debugDepth: 0,
        debugFrames: Object.freeze([]),
        asyncParents: captureAsyncCausality(ctx)
    };
}

function pauseForExceptionState(state, stage) {
    if (state === null || !exceptionFilters[stage] || state.frames.length === 0) {
        return;
    }
    const reportedProperty = `${stage}Reported`;
    if (state[reportedProperty]) {
        return;
    }
    state[reportedProperty] = true;
    // An exception stop supersedes the step operation which led to it. Leaving the
    // plan armed would allow Continue from the exception to report a stale step stop.
    stepPlan = null;
    const origin = state.frames[0];
    const stop = Object.freeze({
        sequence: ++stopSequence,
        reason: "exception",
        exceptionStage: stage,
        exceptionType: state.exceptionType,
        breakpointIds: Object.freeze([]),
        frames: state.frames,
        moduleId: origin.moduleId,
        methodToken: origin.methodToken,
        blockIndex: origin.blockIndex,
        instructionIndex: origin.instructionIndex
    });
    lastStop = stop;
    currentStop = stop;
    debugger;
    endStop(stop.sequence);
}

function pauseForException(opts, error, stage) {
    pauseForExceptionState(getExceptionState(opts, error), stage);
}

export function pauseAtThrownException(opts, error) {
    if (exceptionFilters.thrown || exceptionFilters.unhandled) {
        getExceptionState(opts, error);
    }
    pauseForException(opts, error, "thrown");
}

export function pauseAtUnhandledException(opts, error) {
    pauseForException(opts, error, "unhandled");
}

export function reportUnhandledTaskException(exception) {
    const state = exception !== null && typeof exception === "object"
        ? exceptionStatesByValue.get(exception) ?? null
        : null;
    pauseForExceptionState(state, "unhandled");
}

export function completeException(error) {
    if (error !== null && typeof error === "object") {
        exceptionStates.delete(error);
    }
}

function beginInstruction(opts) {
    const location = readLocation(opts);
    let reason = null;
    let breakpointIds = [];

    if (pauseRequested) {
        pauseRequested = false;
        reason = "pause";
    } else {
        breakpointIds = breakpointLocations.get(locationKey(
            location.moduleId,
            location.methodToken,
            location.blockIndex,
            location.instructionIndex)) ?? [];
        if (breakpointIds.length > 0) {
            reason = "breakpoint";
        }
    }

    if (reason !== null && stepPlan !== null) {
        // A managed pause or breakpoint supersedes the pending managed step.
        // Native JavaScript stops never enter this hook and therefore leave the
        // plan armed for the proxy to resume after the native stop.
        stepPlan = null;
    }

    if (reason === null && stepPlan !== null) {
        const physicalDepth = opts.ctx?.debugDepth;
        if (!Number.isInteger(physicalDepth) || physicalDepth <= 0) {
            throw new Error("Managed stepping requires a positive managed frame depth.");
        }
        const logicalParentCount = Array.isArray(opts.ctx?.asyncParents)
            ? opts.ctx.asyncParents.length
            : 0;
        const depth = physicalDepth + logicalParentCount;
        const depthMatches = stepPlan.kind === "in"
            || (stepPlan.kind === "over" && depth <= stepPlan.baselineDepth)
            || (stepPlan.kind === "out" && depth < stepPlan.baselineDepth);
        if (depthMatches && stepPlan.locations.has(locationKey(
            location.moduleId,
            location.methodToken,
            location.blockIndex,
            location.instructionIndex))) {
            reason = "step";
            stepPlan = null;
        }
    }

    if (reason === null) {
        enabled = debuggerIsEnabled();
        return null;
    }

    const stop = Object.freeze({
        sequence: ++stopSequence,
        reason,
        breakpointIds,
        ...location
    });
    lastStop = stop;
    currentStop = Object.freeze({
        ...stop,
        frame: opts
    });
    return stop;
}

function endStop(sequence) {
    if (currentStop?.sequence === sequence) {
        currentStop = null;
    }
    enabled = debuggerIsEnabled();
}

export function pauseAtManagedInstruction(opts) {
    const stop = beginInstruction(opts);
    if (stop === null) {
        return;
    }

    // CDP observes currentStop and the live opts frame while V8 is suspended here.
    debugger;
    endStop(stop.sequence);
}

export const managedDebugger = Object.freeze({
    get currentStop() {
        return currentStop;
    },
    get enabled() {
        return enabled;
    },
    get lastStop() {
        return lastStop;
    },
    get pauseRequested() {
        return pauseRequested;
    },
    get updateState() {
        return updateState;
    },
    applyUpdate,
    configure,
    disable,
    initializeUpdates,
    waitForDebuggerConfiguration,
    requestPause,
    requestStep,
    reportUnhandledTaskException,
    setExceptionBreakpoints,
    setBreakpoints
});

const runtimeHooks = Object.freeze({
    isInstructionCheckEnabled: () => enabled,
    pauseAtManagedInstruction,
    pauseAtThrownException,
    pauseAtUnhandledException,
    completeException,
    createAsyncContinuationContext
});

function ensureRuntimeHooksInstalled() {
    installManagedDebuggerHooks(runtimeHooks);
}

Object.defineProperty(globalThis, "__FrontEndCSharpDebugger", {
    configurable: false,
    enumerable: false,
    value: managedDebugger,
    writable: false
});

const pendingExceptionFilters = globalThis.__FrontEndCSharpManagedExceptionFilters;
if (pendingExceptionFilters !== undefined) {
    delete globalThis.__FrontEndCSharpManagedExceptionFilters;
    managedDebugger.setExceptionBreakpoints(pendingExceptionFilters);
}

const initialConfiguration = globalThis.__FrontEndCSharpDebuggerInitialConfiguration;
if (initialConfiguration !== undefined) {
    delete globalThis.__FrontEndCSharpDebuggerInitialConfiguration;
    await managedDebugger.configure(initialConfiguration);
}
