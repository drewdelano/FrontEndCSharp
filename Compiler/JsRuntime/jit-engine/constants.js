import { wrap } from "../msil-type-helpers.js";

function push(opts, value, typeName) {
    opts.stack.push(wrap(value, typeName));
}

export const instructionHandlers = Object.freeze({
    "ldc.i4.0": opts => push(opts, 0, "System.Int32"),
    "ldc.i4.1": opts => push(opts, 1, "System.Int32"),
    "ldc.i4.2": opts => push(opts, 2, "System.Int32"),
    "ldc.i4.3": opts => push(opts, 3, "System.Int32"),
    "ldc.i4.4": opts => push(opts, 4, "System.Int32"),
    "ldc.i4.5": opts => push(opts, 5, "System.Int32"),
    "ldc.i4.6": opts => push(opts, 6, "System.Int32"),
    "ldc.i4.7": opts => push(opts, 7, "System.Int32"),
    "ldc.i4.8": opts => push(opts, 8, "System.Int32"),
    "ldc.i4.m1": opts => push(opts, -1, "System.Int32"),
    "ldc.i4": opts => push(opts, opts.instruction.opr, "System.Int32"),
    "ldc.i4.s": opts => push(opts, opts.instruction.opr, "System.Int32"),
    "ldc.i8": opts => push(opts, BigInt(opts.instruction.opr), "System.Int64"),
    "ldc.r4": opts => push(opts, opts.instruction.opr, "System.Single"),
    "ldc.r8": opts => push(opts, opts.instruction.opr, "System.Double"),
    "ldstr": opts => push(opts, opts.instruction.opr, "System.String"),
    "ldnull": opts => push(opts, null, "System.Object")
});
