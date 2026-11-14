function loadLocal(opts, index) {
    opts.stack.push(opts.locals[index].value);
}

function loadLocalAddress(opts) {
    opts.stack.push(opts.locals[opts.instruction.opr]);
}

function storeLocal(opts, index) {
    opts.locals[index].value = opts.stack.pop();
}

export const instructionHandlers = Object.freeze({
    "ldloc.0": opts => loadLocal(opts, 0),
    "ldloc.1": opts => loadLocal(opts, 1),
    "ldloc.2": opts => loadLocal(opts, 2),
    "ldloc.3": opts => loadLocal(opts, 3),
    "ldloc": opts => loadLocal(opts, opts.instruction.opr),
    "ldloc.s": opts => loadLocal(opts, opts.instruction.opr),
    "ldloca": loadLocalAddress,
    "ldloca.s": loadLocalAddress,
    "stloc.0": opts => storeLocal(opts, 0),
    "stloc.1": opts => storeLocal(opts, 1),
    "stloc.2": opts => storeLocal(opts, 2),
    "stloc.3": opts => storeLocal(opts, 3),
    "stloc": opts => storeLocal(opts, opts.instruction.opr),
    "stloc.s": opts => storeLocal(opts, opts.instruction.opr)
});
