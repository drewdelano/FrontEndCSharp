import { unwrap } from "../msil-type-helpers.js";
import { compareUnsignedOrUnordered } from "./unsigned-comparisons.js";

const branch = opts => updateOpts(opts, opts.instruction.opr);
const branchWhen = predicate => opts => {
    if (predicate(opts.stack.pop())) branch(opts);
};
const compareAndBranch = comparator => opts => {
    const right = opts.stack.pop();
    const left = opts.stack.pop();
    if (comparator(left, right)) branch(opts);
};
const compare = comparator => (left, right) => comparator(unwrap(left), unwrap(right));
const compareUnsigned = comparator => (left, right) => compareUnsignedOrUnordered(left, right, comparator);
const leave = opts => opts.beginLeave(opts.instruction.opr);

const branchTrue = branchWhen(isBranchTrue);
const branchFalse = branchWhen(value => !isBranchTrue(value));
const beq = compareAndBranch(compare((left, right) => left === right));
const blt = compareAndBranch(compare((left, right) => left < right));
const ble = compareAndBranch(compare((left, right) => left <= right));
const bgt = compareAndBranch(compare((left, right) => left > right));
const bge = compareAndBranch(compare((left, right) => left >= right));
const bne = compareAndBranch(compare((left, right) => left !== right));
const bleUnsigned = compareAndBranch(compareUnsigned((left, right) => left <= right));
const bgtUnsigned = compareAndBranch(compareUnsigned((left, right) => left > right));
const bgeUnsigned = compareAndBranch(compareUnsigned((left, right) => left >= right));
const bltUnsigned = compareAndBranch(compareUnsigned((left, right) => left < right));

function switchBranch(opts) {
    const value = unwrap(opts.stack.pop());
    const targets = opts.instruction.opr;
    if (!Array.isArray(targets)) throw new TypeError("The IL switch operand must be an array of target offsets.");
    if (!Number.isInteger(value)) throw new TypeError("The IL switch value must be a native-size integer.");
    if (value >= 0 && value < targets.length) updateOpts(opts, targets[value]);
}

export const instructionHandlers = Object.freeze({
    br: branch, 'br.s': branch,
    brtrue: branchTrue, 'brtrue.s': branchTrue,
    brfalse: branchFalse, 'brfalse.s': branchFalse,
    beq, 'beq.s': beq,
    blt, 'blt.s': blt,
    ble, 'ble.s': ble,
    bgt, 'bgt.s': bgt,
    bge, 'bge.s': bge,
    bne, 'bne.s': bne, 'bne.un': bne, 'bne.un.s': bne,
    'ble.un': bleUnsigned, 'ble.un.s': bleUnsigned,
    'bgt.un': bgtUnsigned, 'bgt.un.s': bgtUnsigned,
    'bge.un': bgeUnsigned, 'bge.un.s': bgeUnsigned,
    'blt.un': bltUnsigned, 'blt.un.s': bltUnsigned,
    leave, 'leave.s': leave,
    switch: switchBranch
});

function updateOpts(opts, targetOffset) {
    opts.currentBlockIndex = opts.blocks.findIndex(block => block.ilOffset === targetOffset);
    opts.instructionIndex = 0;
}

function isBranchTrue(wrapped) {
    if (wrapped === null) return false;
    const value = unwrap(wrapped);
    if (value === null) return false;
    switch (typeof value) {
        case 'number':
        case 'bigint': return value !== 0 && value !== 0n;
        case 'boolean': return value;
        case 'object':
        case 'function': return true;
        case 'string': return wrapped?.t === 'System.Char' ? value !== '\0' : true;
        default: throw new TypeError('brtrue/brfalse requires an integer or object reference.');
    }
}
