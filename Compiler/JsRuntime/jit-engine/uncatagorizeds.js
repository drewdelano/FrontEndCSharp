function duplicate(opts) {
    opts.stack.push(opts.stack[opts.stack.length - 1]);
}

function returnFromMethod(opts) {
    opts.returnValue = opts.stack.length === 0 ? undefined : opts.stack.pop();
    opts.returned = true;
}

export const instructionHandlers = Object.freeze({
    "nop": () => {},
    "pop": opts => opts.stack.pop(),
    "dup": duplicate,
    "ret": returnFromMethod
});
