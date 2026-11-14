import assert from "node:assert/strict";
import test from "node:test";

import { executeInstruction } from "../../JsRuntime/jit-engine/_executeInstruction.js";
import { DotNetError, unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";
import { ExecuteMSIL } from "../../JsRuntime/msil.js";

const context = { stackTrace: "   at Test.MissingMember()" };

function execute(opcode, stack, operand) {
    executeInstruction({
        ctx: context,
        genericParams: {},
        instruction: { oc: opcode, opr: operand },
        stack
    });
    return stack;
}

function assertMissingMember(action, typeName, message, hResult) {
    assert.throws(
        action,
        error => {
            assert.ok(error instanceof DotNetError);
            assert.equal(error.name, typeName);
            assert.equal(error.message, message);
            assert.equal(unwrap(error.dotNetException.v._HResult), hResult);
            assert.equal(unwrap(error.dotNetException.v._stackTraceString), context.stackTrace);
            return true;
        }
    );
}

test("direct and virtual calls throw MissingMethodException when runtime lookup fails", () => {
    globalThis.DotNet = {
        Methods: {},
        Types: {
            "Tests.Base": { baseType: null },
            "Tests.Derived": { baseType: "Tests.Base" }
        }
    };
    const method = {
        typeName: "Tests.Base",
        methodName: "Missing()",
        paramTypes: [],
        hasThis: true
    };

    for (const opcode of ["call", "callvirt"]) {
        assertMissingMember(
            () => execute(opcode, [wrap({}, "Tests.Derived")], method),
            "System.MissingMethodException",
            opcode === "callvirt"
                ? "Managed method 'Tests.Base.Missing()' was not emitted for runtime receiver 'Tests.Derived'."
                : "Managed method 'Tests.Base.Missing()' was not emitted.",
            -2146233069
        );
    }
});

test("a function pointer throws MissingMethodException when its target cannot be resolved", () => {
    globalThis.DotNet = { Methods: {}, Types: {} };
    const method = {
        typeName: "Tests.Target",
        methodName: "Missing()",
        paramTypes: [],
        returnType: "System.Void",
        hasThis: false
    };
    const stack = execute("ldftn", [], method);

    assertMissingMember(
        () => execute("calli", stack, null),
        "System.MissingMethodException",
        "Managed method 'Tests.Target.Missing()' was not emitted.",
        -2146233069
    );
});

test("instance field instructions throw MissingFieldException when metadata lookup fails", () => {
    globalThis.DotNet = {
        Methods: {},
        Types: { "Tests.Target": { instances: {}, staticsDefinition: {}, statics: {} } }
    };
    const field = { typeName: "Tests.Target", fieldName: "Missing" };

    for (const [opcode, stack] of [
        ["ldfld", [wrap({}, "Tests.Target")]],
        ["ldflda", [wrap({}, "Tests.Target")]],
        ["stfld", [wrap({}, "Tests.Target"), wrap(1, "System.Int32")]]
    ]) {
        assertMissingMember(
            () => execute(opcode, stack, field),
            "System.MissingFieldException",
            "Attempted to access a non-existing field.",
            -2146233071
        );
    }
});

test("static field instructions throw MissingFieldException when metadata lookup fails", () => {
    globalThis.DotNet = {
        Methods: {},
        Types: { "Tests.Target": { instances: {}, staticsDefinition: {}, statics: {} } }
    };
    const field = { typeName: "Tests.Target", fieldName: "Missing" };

    assertMissingMember(
        () => execute("ldsfld", [], field),
        "System.MissingFieldException",
        "Attempted to access a non-existing field.",
        -2146233071
    );
    assertMissingMember(
        () => execute("stsfld", [wrap(1, "System.Int32")], field),
        "System.MissingFieldException",
        "Attempted to access a non-existing field.",
        -2146233071
    );
});

test("missing member exceptions are catchable by managed exception handlers", () => {
    const cases = [
        {
            catchType: "System.MissingMethodException",
            instruction: {
                oc: "call",
                opr: {
                    typeName: "Tests.Target",
                    methodName: "Missing()",
                    paramTypes: [],
                    hasThis: false
                }
            }
        },
        {
            catchType: "System.MissingFieldException",
            instruction: {
                oc: "ldsfld",
                opr: { typeName: "Tests.Target", fieldName: "Missing" }
            }
        }
    ];

    for (const { catchType, instruction } of cases) {
        globalThis.DotNet = {
            Methods: {},
            Types: { "Tests.Target": { statics: {}, staticsDefinition: {}, instances: {} } }
        };
        const result = ExecuteMSIL(
            context,
            {
                fullMethodName: "Tests.Program.CatchMissingMember()",
                paramType: [],
                localTypes: [],
                hasThis: false,
                exceptionHandlers: [{
                    Flags: "Clause",
                    CatchType: catchType,
                    TryStartBlockIndex: 0,
                    TryEndBlockIndex: 1,
                    HandlerStartBlockIndex: 1,
                    HandlerEndBlockIndex: 2
                }]
            },
            [
                { ilOffset: 0, instructions: [instruction] },
                {
                    ilOffset: 10,
                    instructions: [
                        { oc: "pop", opr: null },
                        { oc: "ldc.i4.1", opr: null },
                        { oc: "ret", opr: null }
                    ]
                }
            ],
            []
        );
        assert.equal(unwrap(result), 1);
    }
});
