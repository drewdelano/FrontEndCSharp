import assert from "node:assert/strict";
import test from "node:test";

import {
    DotNetError,
    throwDotNetException,
    unwrap,
    wrap
} from "../../JsRuntime/msil-type-helpers.js";
import { ExecuteMSIL } from "../../JsRuntime/msil.js";
import { createManagedException } from "../../JsRuntime/managed-exceptions.js";

test("a thrown .NET exception is a JavaScript Error containing the original exception", () => {
    const dotNetException = wrap(
        {
            _message: wrap("Something failed", "System.String"),
            _stackTraceString: null
        },
        "System.InvalidOperationException"
    );

    assert.throws(
        () => throwDotNetException({ stackTrace: "   at Program.Fail()" }, dotNetException),
        error => {
            assert.ok(error instanceof Error);
            assert.ok(error instanceof DotNetError);
            assert.equal(error.name, "System.InvalidOperationException");
            assert.equal(error.message, "Something failed");
            assert.strictEqual(error.dotNetException, dotNetException);
            assert.strictEqual(error.cause, dotNetException);
            assert.equal(dotNetException.v._stackTraceString.v, "   at Program.Fail()");
            return true;
        }
    );
});

test("runtime-created managed exceptions preserve their inner exception", () => {
    const innerException = createManagedException("System.InvalidOperationException", "inner");
    const outerException = createManagedException(
        "System.TypeInitializationException",
        "outer",
        innerException,
        { _typeName: wrap("Example.Type", "System.String") });

    assert.equal(outerException.t, "System.TypeInitializationException");
    assert.equal(outerException.v._message.v, "outer");
    assert.strictEqual(outerException.v._innerException, innerException);
    assert.equal(unwrap(outerException.v._typeName), "Example.Type");
    assert.equal(outerException.v._stackTraceString, null);
});

test("runtime-created managed exceptions use CLR default messages and HResults", () => {
    const expectations = [
        ["System.ArgumentException", "Value does not fall within the expected range.", -2147024809],
        ["System.ArgumentNullException", "Value cannot be null.", -2147467261],
        ["System.ArgumentOutOfRangeException", "Specified argument was out of the range of valid values.", -2146233086],
        ["System.ArithmeticException", "Overflow or underflow in the arithmetic operation.", -2147024362],
        ["System.ArrayTypeMismatchException", "Attempted to access an element as a type incompatible with the array.", -2146233085],
        ["System.DivideByZeroException", "Attempted to divide by zero.", -2147352558],
        ["System.IndexOutOfRangeException", "Index was outside the bounds of the array.", -2146233080],
        ["System.InvalidCastException", "Specified cast is not valid.", -2147467262],
        ["System.MissingFieldException", "Attempted to access a non-existing field.", -2146233071],
        ["System.MissingMethodException", "Attempted to access a missing method.", -2146233069],
        ["System.NullReferenceException", "Object reference not set to an instance of an object.", -2147467261],
        ["System.OverflowException", "Arithmetic operation resulted in an overflow.", -2146233066],
        ["System.Threading.Tasks.TaskCanceledException", "A task was canceled.", -2146233029]
    ];

    for (const [typeName, message, hResult] of expectations) {
        const exception = createManagedException(typeName);
        assert.equal(unwrap(exception.v._message), message, typeName);
        assert.equal(unwrap(exception.v._HResult), hResult, typeName);
        assert.equal(exception.v._innerException, null, typeName);
    }
});

test("a native JavaScript error runs .NET finally handlers before escaping", () => {
    let finallyRan = false;
    const originalDotNet = globalThis.DotNet;
    globalThis.DotNet = {
        Methods: {
            "Test.MarkFinally()": () => {
                finallyRan = true;
            }
        },
        Types: {}
    };

    const methodInfo = {
        fullMethodName: "Test.NativeFailure()",
        paramType: [],
        localTypes: [],
        hasThis: false,
        exceptionHandlers: [{
            Flags: "Finally",
            TryStartBlockIndex: 0,
            TryEndBlockIndex: 1,
            HandlerStartBlockIndex: 1,
            HandlerEndBlockIndex: 2
        }]
    };
    const blocks = [
        {
            ilOffset: 0,
            instructions: [{ oc: "missing.opcode", opr: null }]
        },
        {
            ilOffset: 10,
            instructions: [
                {
                    oc: "call",
                    opr: {
                        typeName: "Test",
                        methodName: "MarkFinally()",
                        paramTypes: [],
                        hasThis: false
                    }
                },
                { oc: "endfinally", opr: null }
            ]
        }
    ];

    try {
        assert.throws(
            () => ExecuteMSIL({}, methodInfo, blocks, []),
            /Missing instruction handling: missing\.opcode/
        );
        assert.equal(finallyRan, true);
    } finally {
        globalThis.DotNet = originalDotNet;
    }
});

test("fault handlers run during exceptional unwind but not normal leave", () => {
    let faultRuns = 0;
    let normalContinuationRan = false;
    const originalDotNet = globalThis.DotNet;
    globalThis.DotNet = {
        Methods: {
            "Test.MarkFault()": () => {
                faultRuns++;
            },
            "Test.MarkNormalContinuation()": () => {
                normalContinuationRan = true;
            }
        },
        Types: {}
    };

    const methodInfo = {
        fullMethodName: "Test.FaultHandler()",
        paramType: [],
        localTypes: [],
        hasThis: false,
        exceptionHandlers: [{
            Flags: "Fault",
            TryStartBlockIndex: 0,
            TryEndBlockIndex: 1,
            HandlerStartBlockIndex: 1,
            HandlerEndBlockIndex: 2
        }]
    };
    const faultBlock = {
        ilOffset: 10,
        instructions: [
            {
                oc: "call",
                opr: {
                    typeName: "Test",
                    methodName: "MarkFault()",
                    paramTypes: [],
                    hasThis: false
                }
            },
            { oc: "endfinally", opr: null }
        ]
    };

    try {
        assert.throws(
            () => ExecuteMSIL(
                {},
                methodInfo,
                [
                    { ilOffset: 0, instructions: [{ oc: "missing.opcode", opr: null }] },
                    faultBlock
                ],
                []),
            /Missing instruction handling: missing\.opcode/
        );
        assert.equal(faultRuns, 1);

        ExecuteMSIL(
            {},
            methodInfo,
            [
                { ilOffset: 0, instructions: [{ oc: "leave", opr: 20 }] },
                faultBlock,
                {
                    ilOffset: 20,
                    instructions: [
                        {
                            oc: "call",
                            opr: {
                                typeName: "Test",
                                methodName: "MarkNormalContinuation()",
                                paramTypes: [],
                                hasThis: false
                            }
                        },
                        { oc: "ret", opr: null }
                    ]
                }
            ],
            []);
        assert.equal(faultRuns, 1);
        assert.equal(normalContinuationRan, true);
    } finally {
        globalThis.DotNet = originalDotNet;
    }
});
