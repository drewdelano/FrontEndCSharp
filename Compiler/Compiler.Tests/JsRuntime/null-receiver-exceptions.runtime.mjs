import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";
import { invokeDelegate } from "../../JsRuntime/jit-engine/calls.js";
import { DotNetError, wrap } from "../../JsRuntime/msil-type-helpers.js";

globalThis.DotNet = {
    Methods: {},
    Types: {
        "System.Object": { isClass: true, baseType: null, interfaces: [] },
        "System.String": { isClass: true, baseType: "System.Object", interfaces: [] }
    }
};

const context = { stackTrace: "   at Test.NullReceiver()" };

function execute(opcode, stack, operand = null) {
    executeInstruction({
        ctx: context,
        genericParams: {},
        instruction: { oc: opcode, opr: operand },
        stack
    });
    return stack;
}

function assertNullReference(action) {
    assert.throws(
        action,
        error => {
            assert.ok(error instanceof DotNetError);
            assert.equal(error.name, "System.NullReferenceException");
            assert.equal(error.dotNetException.t, "System.NullReferenceException");
            assert.equal(error.dotNetException.v._stackTraceString.v, "   at Test.NullReceiver()");
            return true;
        }
    );
}

test("callvirt and ldvirtftn throw NullReferenceException for either managed-null representation", () => {
    const method = {
        typeName: "Tests.Target",
        methodName: "GetValue()",
        paramTypes: [],
        returnType: "System.Int32",
        hasThis: true
    };

    for (const value of [null, wrap(null, "Tests.Target")]) {
        assertNullReference(() => execute("callvirt", [value], method));
        assertNullReference(() => execute("ldvirtftn", [value], method));
    }
});

test("delegate invocation throws NullReferenceException for a null delegate", () => {
    const invocation = {
        methodName: "Invoke()",
        paramTypes: [],
        hasThis: true,
        isDelegateInvocation: true
    };
    assertNullReference(() => execute("callvirt", [wrap(null, "System.Action")], invocation));
    assertNullReference(() => invokeDelegate(context, null));
});

test("instance field instructions throw NullReferenceException for null receivers", () => {
    const field = { fieldName: "Value" };
    assertNullReference(() => execute("ldfld", [wrap(null, "Tests.Target")], field));
    assertNullReference(() => execute("ldflda", [{ value: null }], field));
    assertNullReference(() => execute("stfld", [null, wrap(1, "System.Int32")], field));
});

test("unbox and value-type unbox.any throw NullReferenceException for null", () => {
    assertNullReference(() => execute("unbox", [wrap(null, "System.Object")], { typeName: "System.Int32" }));
    assertNullReference(() => execute("unbox.any", [null], { typeName: "System.Int32" }));
});

test("reference-type unbox.any preserves null", () => {
    assert.deepEqual(
        execute("unbox.any", [wrap(null, "System.Object")], { typeName: "System.String" }),
        [null]
    );
});

test("throw null throws NullReferenceException", () => {
    assertNullReference(() => execute("throw", [null]));
    assertNullReference(() => execute("throw", [wrap(null, "System.Exception")]));
});
