function loadArgument(opts, index) {
    opts.stack.push(opts.methodArgs[index]);
}

function loadArgumentAddress(opts) {
    const index = opts.instruction.opr;
    const argument = opts.methodArgs[index];
    if (typeof argument === "object" && argument !== null && "value" in argument) {
        opts.stack.push(argument);
        return;
    }
    const reference = {};
    Object.defineProperty(reference, "value", {
        enumerable: true,
        get: () => opts.methodArgs[index],
        set: value => { opts.methodArgs[index] = value; }
    });
    opts.stack.push(reference);
}

function storeArgument(opts, index) {
    const value = opts.stack.pop();
    const argument = opts.methodArgs[index];
    if (typeof argument === "object" && argument !== null && "value" in argument) {
        argument.value = value;
    } else {
        opts.methodArgs[index] = value;
    }
}

export const instructionHandlers = Object.freeze({
    "ldarg.0": opts => loadArgument(opts, 0),
    "ldarg.1": opts => loadArgument(opts, 1),
    "ldarg.2": opts => loadArgument(opts, 2),
    "ldarg.3": opts => loadArgument(opts, 3),
    "ldarg": opts => loadArgument(opts, opts.instruction.opr),
    "ldarg.s": opts => loadArgument(opts, opts.instruction.opr),
    "ldarga": loadArgumentAddress,
    "ldarga.s": loadArgumentAddress,
    "starg.0": opts => storeArgument(opts, 0),
    "starg.1": opts => storeArgument(opts, 1),
    "starg.2": opts => storeArgument(opts, 2),
    "starg.3": opts => storeArgument(opts, 3),
    "starg": opts => storeArgument(opts, opts.instruction.opr),
    "starg.s": opts => storeArgument(opts, opts.instruction.opr)
});
