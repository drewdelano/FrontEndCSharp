import assert from "node:assert/strict";
import test from "node:test";

import {
    createAsyncContinuationContext,
    completeException,
    managedDebugger,
    pauseAtManagedInstruction,
    pauseAtThrownException,
    pauseAtUnhandledException,
    reportUnhandledTaskException,
    waitForDebuggerConfiguration
} from "../../JsRuntime/managed-debugger.js";
import { wrap } from "../../JsRuntime/msil-type-helpers.js";
import { ExecuteMSIL } from "../../JsRuntime/msil.js";

const moduleId = "98fd7728-bb4f-4b3c-adb9-a62f4ad083ac";
const methodToken = 0x06000001;

function createMethodInfo() {
    return {
        debugModuleId: moduleId,
        debugMethodToken: methodToken,
        fullMethodName: "Tests.Debugger.Target()",
        paramType: [],
        localTypes: [],
        hasThis: false,
        exceptionHandlers: []
    };
}

function createBlocks() {
    return [{
        ilOffset: 0,
        instructions: [
            { oc: "nop" },
            { oc: "ret" }
        ]
    }];
}

test.afterEach(() => {
    managedDebugger.disable();
});

test("debugger rendezvous fails visibly instead of waiting indefinitely", async () => {
    const originalError = console.error;
    const diagnostics = [];
    console.error = message => diagnostics.push(message);
    try {
        await assert.rejects(
            waitForDebuggerConfiguration("unconfigured-build", 5),
            /did not attach within 5 milliseconds/);
        assert.deepEqual(diagnostics, [
            "FrontEndCSharp debugger did not attach within 5 milliseconds; managed entry-point execution was canceled."
        ]);
    } finally {
        console.error = originalError;
    }
});

test("disabled debugger does not record instruction stops", () => {
    ExecuteMSIL({}, createMethodInfo(), createBlocks(), []);

    assert.equal(managedDebugger.currentStop, null);
    assert.equal(managedDebugger.lastStop, null);
});

test("one invocation reuses its context and managed-frame array across instructions", () => {
    const originalDotNet = globalThis.DotNet;
    const contexts = [];
    const frameArrays = [];
    const stackTraces = [];
    globalThis.DotNet = {
        ...originalDotNet,
        Methods: {
            ...originalDotNet?.Methods,
            "Tests.Debugger.CaptureContext()": ctx => {
                contexts.push(ctx);
                frameArrays.push(ctx.debugFrames);
                stackTraces.push(ctx.stackTrace);
            }
        }
    };
    const captureMethod = {
        typeName: "Tests.Debugger",
        methodName: "CaptureContext()",
        paramTypes: [],
        hasThis: false
    };
    const blocks = [{
        ilOffset: 0,
        instructions: [
            { oc: "call", opr: captureMethod },
            { oc: "call", opr: captureMethod },
            { oc: "ret" }
        ]
    }];

    try {
        ExecuteMSIL({}, createMethodInfo(), blocks, []);

        assert.equal(contexts.length, 2);
        assert.equal(contexts[0], contexts[1]);
        assert.equal(frameArrays[0], frameArrays[1]);
        assert.match(stackTraces[0], /Block: 0, Instruction: 0/);
        assert.match(stackTraces[1], /Block: 0, Instruction: 1/);
    } finally {
        globalThis.DotNet = originalDotNet;
    }
});

test("an invocation does not format its parent stack until the stack trace is consumed", () => {
    let stackTraceReads = 0;
    const parentContext = {};
    Object.defineProperty(parentContext, "stackTrace", {
        enumerable: true,
        get() {
            stackTraceReads++;
            return "   at Tests.Debugger.Parent()";
        }
    });

    ExecuteMSIL(parentContext, createMethodInfo(), createBlocks(), []);

    assert.equal(stackTraceReads, 0);
});

test("generic call context does not leak into a later call in the same invocation", () => {
    const originalDotNet = globalThis.DotNet;
    const observedGenericParameters = [];
    globalThis.DotNet = {
        ...originalDotNet,
        Methods: {
            ...originalDotNet?.Methods,
            "Tests.Debugger.CaptureGeneric()": ctx => {
                observedGenericParameters.push(ctx.genericParams);
            },
            "Tests.Debugger.CapturePlain()": ctx => {
                observedGenericParameters.push(ctx.genericParams);
            }
        }
    };
    const blocks = [{
        ilOffset: 0,
        instructions: [
            {
                oc: "call",
                opr: {
                    typeName: "Tests.Debugger",
                    methodName: "CaptureGeneric()",
                    paramTypes: [],
                    hasThis: false,
                    genericMethodParams: { T: "System.Int32" }
                }
            },
            {
                oc: "call",
                opr: {
                    typeName: "Tests.Debugger",
                    methodName: "CapturePlain()",
                    paramTypes: [],
                    hasThis: false
                }
            },
            { oc: "ret" }
        ]
    }];

    try {
        ExecuteMSIL({}, createMethodInfo(), blocks, []);

        assert.deepEqual(observedGenericParameters, [
            { T: "System.Int32" },
            undefined
        ]);
    } finally {
        globalThis.DotNet = originalDotNet;
    }
});

test("managed breakpoint stops before the matching runtime instruction", () => {
    managedDebugger.setBreakpoints([{
        id: 17,
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    }]);

    ExecuteMSIL({}, createMethodInfo(), createBlocks(), []);

    assert.equal(managedDebugger.currentStop, null);
    assert.deepEqual(managedDebugger.lastStop, {
        sequence: 1,
        reason: "breakpoint",
        breakpointIds: [17],
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    });
});

test("pause request is consumed at the next instruction boundary", () => {
    managedDebugger.requestPause();

    ExecuteMSIL({}, createMethodInfo(), createBlocks(), []);

    assert.equal(managedDebugger.pauseRequested, false);
    assert.deepEqual(managedDebugger.lastStop, {
        sequence: 1,
        reason: "pause",
        breakpointIds: [],
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 0
    });
});

test("step in stops at the next requested visible sequence point", () => {
    managedDebugger.requestStep("in", 1, [{
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    }]);

    ExecuteMSIL({}, createMethodInfo(), createBlocks(), []);

    assert.equal(managedDebugger.lastStop.reason, "step");
    assert.equal(managedDebugger.lastStop.instructionIndex, 1);
});

test("step over skips deeper managed frames and stops at the baseline depth", () => {
    const opts = createExceptionOpts();
    const location = {
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    };
    managedDebugger.requestStep("over", 2, [location]);

    opts.ctx.debugDepth = 3;
    pauseAtManagedInstruction(opts);
    assert.equal(managedDebugger.lastStop, null);

    opts.ctx.debugDepth = 2;
    pauseAtManagedInstruction(opts);
    assert.equal(managedDebugger.lastStop.reason, "step");
});

test("step out skips the baseline frame and stops in its managed caller", () => {
    const opts = createExceptionOpts();
    const location = {
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    };
    managedDebugger.requestStep("out", 2, [location]);

    opts.ctx.debugDepth = 2;
    pauseAtManagedInstruction(opts);
    assert.equal(managedDebugger.lastStop, null);

    opts.ctx.debugDepth = 1;
    pauseAtManagedInstruction(opts);
    assert.equal(managedDebugger.lastStop.reason, "step");
});

test("step over compares logical depth after an await", () => {
    const opts = createExceptionOpts();
    const location = {
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    };
    opts.ctx.asyncParents = [{ isLogical: true }, { isLogical: true }];
    managedDebugger.requestStep("over", 3, [location]);

    opts.ctx.debugDepth = 2;
    pauseAtManagedInstruction(opts);
    assert.equal(managedDebugger.lastStop, null);

    opts.ctx.debugDepth = 1;
    pauseAtManagedInstruction(opts);
    assert.equal(managedDebugger.lastStop.reason, "step");
});

test("step out reaches the logical async caller", () => {
    const opts = createExceptionOpts();
    const location = {
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    };
    opts.ctx.asyncParents = [{ isLogical: true }, { isLogical: true }];
    managedDebugger.requestStep("out", 3, [location]);

    pauseAtManagedInstruction(opts);
    assert.equal(managedDebugger.lastStop, null);

    opts.ctx.asyncParents = [{ isLogical: true }];
    pauseAtManagedInstruction(opts);
    assert.equal(managedDebugger.lastStop.reason, "step");
});

test("step plans reject unsupported kinds", () => {
    assert.throws(
        () => managedDebugger.requestStep("sideways", 1, []),
        /step kind/);
});

test("a managed breakpoint stop cancels a pending managed step", () => {
    const location = {
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    };
    managedDebugger.requestStep("in", 1, [location]);
    managedDebugger.setBreakpoints([{ id: 17, ...location }]);

    ExecuteMSIL({}, createMethodInfo(), createBlocks(), []);
    assert.equal(managedDebugger.lastStop.reason, "breakpoint");
    assert.equal(managedDebugger.lastStop.sequence, 1);

    managedDebugger.setBreakpoints([]);
    ExecuteMSIL({}, createMethodInfo(), createBlocks(), []);

    assert.equal(managedDebugger.lastStop.reason, "breakpoint");
    assert.equal(managedDebugger.lastStop.sequence, 1);
});

test("a managed exception stop cancels a pending managed step", () => {
    managedDebugger.requestStep("in", 1, [{
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    }]);
    managedDebugger.setExceptionBreakpoints(["thrown"]);
    const opts = createExceptionOpts();

    pauseAtThrownException(opts, {
        dotNetException: wrap({}, "Tests.ManagedException")
    });
    assert.equal(managedDebugger.lastStop.reason, "exception");

    pauseAtManagedInstruction(opts);

    assert.equal(managedDebugger.lastStop.reason, "exception");
});

test("a thrown exception stop retains immutable origin values", () => {
    const opts = createExceptionOpts();
    const error = { dotNetException: wrap({}, "Tests.ManagedException") };
    managedDebugger.setExceptionBreakpoints(["thrown"]);

    pauseAtThrownException(opts, error);
    opts.methodArgs[0] = wrap(99, "System.Int32");
    opts.locals[0].value = wrap(100, "System.Int32");

    assert.equal(managedDebugger.lastStop.reason, "exception");
    assert.equal(managedDebugger.lastStop.exceptionStage, "thrown");
    assert.equal(managedDebugger.lastStop.exceptionType, "Tests.ManagedException");
    assert.deepEqual(managedDebugger.lastStop.frames[0].arguments[0], {
        type: "System.Int32",
        value: "7",
        display: 7
    });
    assert.deepEqual(managedDebugger.lastStop.frames[0].locals[0], {
        type: "System.Int32",
        value: "8",
        display: 8
    });
});

test("a managed stop preserves negative zero in debugger display values", () => {
    const opts = createExceptionOpts();
    opts.methodArgs[0] = wrap(-0, "System.Double");
    const error = { dotNetException: wrap({}, "Tests.ManagedException") };
    managedDebugger.setExceptionBreakpoints(["thrown"]);

    pauseAtThrownException(opts, error);

    assert.deepEqual(managedDebugger.lastStop.frames[0].arguments[0], {
        type: "System.Double",
        value: "-0",
        display: "-0"
    });
});

test("an exception reports thrown and unhandled at most once each", () => {
    const opts = createExceptionOpts();
    const error = { dotNetException: wrap({}, "Tests.ManagedException") };
    managedDebugger.setExceptionBreakpoints(["thrown", "unhandled"]);

    pauseAtThrownException(opts, error);
    pauseAtThrownException(opts, error);
    assert.equal(managedDebugger.lastStop.sequence, 1);
    pauseAtUnhandledException(opts, error);
    pauseAtUnhandledException(opts, error);

    assert.equal(managedDebugger.lastStop.sequence, 2);
    assert.equal(managedDebugger.lastStop.exceptionStage, "unhandled");
    completeException(error);
});

test("a faulted host task reports the captured throw origin as unhandled", () => {
    const opts = createExceptionOpts();
    const exception = wrap({}, "Tests.ManagedException");
    const error = { dotNetException: exception };
    managedDebugger.setExceptionBreakpoints(["unhandled"]);

    pauseAtThrownException(opts, error);
    completeException(error);
    reportUnhandledTaskException(exception);

    assert.equal(managedDebugger.lastStop.reason, "exception");
    assert.equal(managedDebugger.lastStop.exceptionStage, "unhandled");
    assert.equal(managedDebugger.lastStop.exceptionType, "Tests.ManagedException");
    assert.equal(managedDebugger.lastStop.frames[0].moduleId, moduleId);
    assert.equal(managedDebugger.lastStop.frames[0].methodToken, methodToken);
});

test("native JavaScript failures do not produce managed exception stops", () => {
    managedDebugger.setExceptionBreakpoints(["thrown", "unhandled"]);
    const opts = createExceptionOpts();

    pauseAtThrownException(opts, new Error("native"));
    pauseAtUnhandledException(opts, new Error("native"));

    assert.equal(managedDebugger.lastStop, null);
});

test("async continuation context snapshots logical callers and resets physical depth", () => {
    const stateMachine = wrap({
        "<beforeAwait>5__1": wrap(42, "System.Int32")
    }, "Tests.StateMachine");
    const current = {
        currentBlockIndex: 1,
        instructionIndex: 2,
        methodArgs: [stateMachine],
        locals: [],
        methodInfo: createMethodInfo()
    };
    const caller = {
        currentBlockIndex: 3,
        instructionIndex: 4,
        methodArgs: [wrap(7, "System.Int32")],
        locals: [{ value: wrap(8, "System.Int32") }],
        methodInfo: {
            ...createMethodInfo(),
            debugMethodToken: 0x06000002
        }
    };
    const inherited = Object.freeze({
        moduleId,
        methodToken: 0x06000003,
        blockIndex: 5,
        instructionIndex: 6,
        arguments: Object.freeze([]),
        locals: Object.freeze([]),
        fields: Object.freeze([]),
        isLogical: true
    });

    const continuation = createAsyncContinuationContext({
        debugDepth: 2,
        debugFrames: [current, caller],
        asyncParents: [inherited]
    });
    caller.methodArgs[0] = wrap(99, "System.Int32");
    caller.locals[0].value = wrap(100, "System.Int32");

    assert.equal(continuation.debugDepth, 0);
    assert.deepEqual(continuation.debugFrames, []);
    assert.equal(continuation.asyncParents.length, 2);
    assert.equal(continuation.asyncParents[0].isLogical, true);
    assert.deepEqual(continuation.asyncParents[0].arguments[0], {
        type: "System.Int32",
        value: "7",
        display: 7
    });
    assert.deepEqual(continuation.asyncParents[0].locals[0], {
        type: "System.Int32",
        value: "8",
        display: 8
    });
    assert.equal(continuation.asyncParents[1], inherited);
});

test("breakpoint configuration replaces the complete previous set", () => {
    managedDebugger.setBreakpoints([{
        id: 1,
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 0
    }]);
    managedDebugger.setBreakpoints([{
        id: 2,
        moduleId,
        methodToken,
        blockIndex: 0,
        instructionIndex: 1
    }]);

    ExecuteMSIL({}, createMethodInfo(), createBlocks(), []);

    assert.equal(managedDebugger.lastStop.instructionIndex, 1);
    assert.deepEqual(managedDebugger.lastStop.breakpointIds, [2]);
});

test("one managed location reports every Visual Studio breakpoint bound to it", () => {
    managedDebugger.setBreakpoints([
        { id: 3, moduleId, methodToken, blockIndex: 0, instructionIndex: 0 },
        { id: 4, moduleId, methodToken, blockIndex: 0, instructionIndex: 0 }
    ]);

    ExecuteMSIL({}, createMethodInfo(), createBlocks(), []);

    assert.deepEqual(managedDebugger.lastStop.breakpointIds, [3, 4]);
});

test("breakpoint configuration rejects malformed managed locations", () => {
    assert.throws(
        () => managedDebugger.setBreakpoints([{
            id: 1,
            moduleId,
            methodToken,
            blockIndex: 0,
            instructionIndex: -1
        }]),
        /non-negative integer instruction index/
    );
});

test("runtime updates are ordered, validated, and committed atomically", () => {
    const firstName = "Tests.Debugger.First()";
    const secondName = "Tests.Debugger.Second()";
    const firstOriginal = () => 1;
    const secondOriginal = () => 2;
    const firstReplacement = () => 10;
    const secondReplacement = () => 20;
    globalThis.DotNet ??= {};
    globalThis.DotNet.Methods ??= {};
    globalThis.DotNet.Methods[firstName] = firstOriginal;
    globalThis.DotNet.Methods[secondName] = secondOriginal;

    const initial = managedDebugger.initializeUpdates("managed-debugger-test-build", "baseline-hash");
    assert.deepEqual(initial, {
        baselineBuildId: "managed-debugger-test-build",
        generation: 0,
        hash: "baseline-hash"
    });

    assert.throws(() => managedDebugger.applyUpdate({
        baselineBuildId: "managed-debugger-test-build",
        expectedGeneration: 0,
        nextGeneration: 1,
        hash: "invalid-update",
        methods: [
            { name: firstName, moduleId, methodToken, implementation: firstReplacement },
            { name: "Tests.Debugger.Missing()", moduleId, methodToken: methodToken + 1, implementation: secondReplacement }
        ]
    }), /unknown method/);
    assert.equal(globalThis.DotNet.Methods[firstName], firstOriginal);
    assert.equal(globalThis.DotNet.Methods[secondName], secondOriginal);
    assert.equal(managedDebugger.updateState.generation, 0);

    const acknowledgement = managedDebugger.applyUpdate({
        baselineBuildId: "managed-debugger-test-build",
        expectedGeneration: 0,
        nextGeneration: 1,
        hash: "generation-1-hash",
        methods: [
            { name: firstName, moduleId, methodToken, implementation: firstReplacement },
            { name: secondName, moduleId, methodToken: methodToken + 1, implementation: secondReplacement }
        ]
    });
    assert.deepEqual(acknowledgement, {
        baselineBuildId: "managed-debugger-test-build",
        generation: 1,
        hash: "generation-1-hash"
    });
    assert.equal(globalThis.DotNet.Methods[firstName](), 10);
    assert.equal(globalThis.DotNet.Methods[secondName](), 20);

    assert.throws(() => managedDebugger.applyUpdate({
        baselineBuildId: "managed-debugger-test-build",
        expectedGeneration: 0,
        nextGeneration: 1,
        hash: "stale-hash",
        methods: [{ name: firstName, moduleId, methodToken, implementation: firstOriginal }]
    }), /stale or out of order/);
    assert.equal(globalThis.DotNet.Methods[firstName], firstReplacement);
    assert.equal(managedDebugger.updateState.generation, 1);

    delete globalThis.DotNet.Methods[firstName];
    delete globalThis.DotNet.Methods[secondName];
});

function createExceptionOpts() {
    const opts = {
        ctx: { debugDepth: 1 },
        currentBlockIndex: 0,
        instructionIndex: 1,
        methodArgs: [wrap(7, "System.Int32")],
        locals: [{ value: wrap(8, "System.Int32") }],
        methodInfo: createMethodInfo()
    };
    opts.ctx.debugFrames = [opts];
    return opts;
}
