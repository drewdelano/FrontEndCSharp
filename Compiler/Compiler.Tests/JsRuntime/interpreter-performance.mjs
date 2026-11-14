import assert from "node:assert/strict";
import { performance } from "node:perf_hooks";

import { managedDebugger } from "../../JsRuntime/managed-debugger.js";
import { ExecuteMSIL } from "../../JsRuntime/msil.js";
import { unwrap } from "../../JsRuntime/msil-type-helpers.js";

const iterationCount = 20_000;
const sampleCount = 7;
const moduleId = "3aa4ec62-9566-457c-b555-b67aa266cd13";
const methodToken = 0x06000001;

const methodInfo = Object.freeze({
    debugModuleId: moduleId,
    debugMethodToken: methodToken,
    fullMethodName: "Benchmarks.Interpreter.MixedInstructions()",
    paramType: [],
    localTypes: [
        { typeName: "System.Int32", genericParams: {}, isFunctionPointer: false },
        { typeName: "System.Int32", genericParams: {}, isFunctionPointer: false }
    ],
    hasThis: false,
    exceptionHandlers: []
});

const blocks = Object.freeze([
    Object.freeze({
        ilOffset: 0,
        instructions: Object.freeze([
            Object.freeze({ oc: "ldc.i4.0" }),
            Object.freeze({ oc: "stloc.0" }),
            Object.freeze({ oc: "ldc.i4.0" }),
            Object.freeze({ oc: "stloc.1" }),
            Object.freeze({ oc: "br", opr: 10 })
        ])
    }),
    Object.freeze({
        ilOffset: 10,
        instructions: Object.freeze([
            Object.freeze({ oc: "ldloc.1" }),
            Object.freeze({ oc: "ldloc.0" }),
            Object.freeze({ oc: "add" }),
            Object.freeze({ oc: "stloc.1" }),
            Object.freeze({ oc: "ldloc.0" }),
            Object.freeze({ oc: "ldc.i4.1" }),
            Object.freeze({ oc: "add" }),
            Object.freeze({ oc: "stloc.0" }),
            Object.freeze({ oc: "ldloc.0" }),
            Object.freeze({ oc: "ldc.i4", opr: iterationCount }),
            Object.freeze({ oc: "blt", opr: 10 })
        ])
    }),
    Object.freeze({
        ilOffset: 20,
        instructions: Object.freeze([
            Object.freeze({ oc: "ldloc.1" }),
            Object.freeze({ oc: "ret" })
        ])
    })
]);

function executeWorkload() {
    const result = ExecuteMSIL({}, methodInfo, blocks, []);
    assert.equal(unwrap(result), iterationCount * (iterationCount - 1) / 2);
}

function median(values) {
    const sorted = [...values].sort((left, right) => left - right);
    return sorted[Math.floor(sorted.length / 2)];
}

function collect(mode, configureDebugger) {
    configureDebugger();
    for (let i = 0; i < 3; i++) {
        executeWorkload();
    }

    const elapsed = [];
    const heapGrowth = [];
    for (let i = 0; i < sampleCount; i++) {
        globalThis.gc?.();
        const heapBefore = process.memoryUsage().heapUsed;
        const started = performance.now();
        executeWorkload();
        elapsed.push(performance.now() - started);
        heapGrowth.push(process.memoryUsage().heapUsed - heapBefore);
    }

    return {
        mode,
        medianMilliseconds: Number(median(elapsed).toFixed(3)),
        medianHeapGrowthBytes: Math.round(median(heapGrowth)),
        samples: sampleCount,
        instructionsPerSample: 7 + iterationCount * 11
    };
}

try {
    const results = [
        collect("debugger-disabled", () => managedDebugger.disable()),
        collect("debugger-enabled-idle", () => managedDebugger.setBreakpoints([{
            id: 1,
            moduleId,
            methodToken,
            blockIndex: 999,
            instructionIndex: 0
        }]))
    ];
    console.log(JSON.stringify({
        node: process.version,
        exposeGc: typeof globalThis.gc === "function",
        iterationCount,
        results
    }, null, 2));
} finally {
    managedDebugger.disable();
}
