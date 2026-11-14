import { wrap, unwrap } from "../msil-type-helpers.js";
import { compareUnsignedOrUnordered } from "./unsigned-comparisons.js";

function popOperands(stack) {
    const right = stack.pop();
    const left = stack.pop();
    return { left, right };
}

function pushBoolean(stack, value) {
    stack.push(wrap(value ? 1 : 0, "System.Int32"));
}

function compareEqual(opts) {
    const { left, right } = popOperands(opts.stack);
    // CLI numeric equality permits the bool/int representations produced on the stack.
    pushBoolean(opts.stack, unwrap(left) == unwrap(right));
}

function compareLessThan(opts) {
    const { left, right } = popOperands(opts.stack);
    pushBoolean(opts.stack, unwrap(left) < unwrap(right));
}

function compareUnsignedLessThan(opts) {
    const { left, right } = popOperands(opts.stack);
    pushBoolean(opts.stack, compareUnsignedOrUnordered(left, right, (l, r) => l < r));
}

function compareGreaterThan(opts) {
    const { left, right } = popOperands(opts.stack);
    pushBoolean(opts.stack, unwrap(left) > unwrap(right));
}

function compareUnsignedGreaterThan(opts) {
    const { left, right } = popOperands(opts.stack);
    const leftValue = unwrap(left);
    const rightValue = unwrap(right);
    const comparesReferences = leftValue === null
        || rightValue === null
        || (typeof leftValue === "object" && leftValue !== null)
        || (typeof rightValue === "object" && rightValue !== null);
    const result = comparesReferences
        ? leftValue !== null && rightValue === null
        : compareUnsignedOrUnordered(left, right, (l, r) => l > r);
    pushBoolean(opts.stack, result);
}

export const instructionHandlers = Object.freeze({
    "ceq": compareEqual,
    "clt": compareLessThan,
    "clt.un": compareUnsignedLessThan,
    "cgt": compareGreaterThan,
    "cgt.un": compareUnsignedGreaterThan
});
