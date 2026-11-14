const unsupportedTypedReference = ({ instruction: { oc } }) => {
    throw new Error(`MSIL opcode '${oc}' requires an addressable-storage or typed-reference model and is not supported.`);
};
const noOperation = () => { };
const constrainNextCall = opts => {
    if (opts.constrainedType !== undefined) {
        throw new Error("A 'constrained.' prefix must be followed immediately by one 'callvirt'.");
    }
    opts.constrainedType = opts.instruction.opr;
};
const loadIndirect = ({ instruction: { oc }, stack }) => {
    stack.push(requireManagedReferenceCell(stack.pop(), oc).value);
};
const storeIndirect = ({ instruction: { oc }, stack }) => {
    const value = stack.pop();
    requireManagedReferenceCell(stack.pop(), oc).value = value;
};

export const instructionHandlers = Object.freeze({
    refanyval: unsupportedTypedReference, mkrefany: unsupportedTypedReference,
    'volatile.': noOperation, 'constrained.': constrainNextCall,
    'ldind.ref': loadIndirect, 'ldind.i4': loadIndirect, 'ldind.i8': loadIndirect,
    'ldind.i1': loadIndirect, 'ldind.i2': loadIndirect, 'ldind.u1': loadIndirect,
    'ldind.u2': loadIndirect, 'ldind.u4': loadIndirect, 'ldind.u8': loadIndirect, ldobj: loadIndirect,
    'stind.ref': storeIndirect, 'stind.i4': storeIndirect, 'stind.i8': storeIndirect,
    'stind.i1': storeIndirect, 'stind.i2': storeIndirect, stobj: storeIndirect
});

function requireManagedReferenceCell(value, opcode) {
    if (typeof value !== 'object' || value === null || !Object.hasOwn(value, 'value')) {
        throw new Error(`MSIL opcode '${opcode}' requires a managed reference cell.`);
    }
    return value;
}
