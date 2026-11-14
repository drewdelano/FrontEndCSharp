import assert from "node:assert/strict";
import test from "node:test";

import {
    createAsyncContinuationContext,
    getManagedDebuggerHooks,
    installManagedDebuggerHooks
} from "../../JsRuntime/managed-debugger-hooks.js";
import { ExecuteMSIL } from "../../JsRuntime/msil.js";

const methodInfo = {
    debugModuleId: "f7585fb3-60cf-423f-b4b7-621f89953421",
    debugMethodToken: 0x06000001,
    fullMethodName: "Tests.RuntimeMode.Target()",
    paramType: [],
    localTypes: [],
    hasThis: false,
    exceptionHandlers: []
};
const blocks = [{
    ilOffset: 0,
    instructions: [{ oc: "nop" }, { oc: "ret" }]
}];

test("normal execution does not install or invoke managed debugger hooks", () => {
    assert.equal(getManagedDebuggerHooks(), null);
    ExecuteMSIL({}, methodInfo, blocks, []);
    assert.equal(getManagedDebuggerHooks(), null);
});

test("normal async continuation context does not load debugger state", () => {
    const context = { marker: "normal" };

    const continuation = createAsyncContinuationContext(context);

    assert.notEqual(continuation, context);
    assert.deepEqual(continuation, context);
    assert.equal(getManagedDebuggerHooks(), null);
});

test("debug execution invokes hooks installed before method entry", () => {
    let instructionChecks = 0;
    const hooks = {
        isInstructionCheckEnabled: () => true,
        pauseAtManagedInstruction: () => instructionChecks++,
        pauseAtThrownException: () => {},
        pauseAtUnhandledException: () => {},
        completeException: () => {},
        createAsyncContinuationContext: context => ({ ...context, debug: true })
    };
    installManagedDebuggerHooks(hooks);

    ExecuteMSIL({}, methodInfo, blocks, []);

    assert.equal(getManagedDebuggerHooks(), hooks);
    assert.equal(instructionChecks, 2);
    assert.deepEqual(createAsyncContinuationContext({ marker: "debug" }), {
        marker: "debug",
        debug: true
    });
});
