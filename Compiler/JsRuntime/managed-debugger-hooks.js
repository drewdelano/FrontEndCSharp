let managedDebuggerHooks = null;

export function getManagedDebuggerHooks() {
    return managedDebuggerHooks;
}

export function installManagedDebuggerHooks(hooks) {
    if (hooks === null || typeof hooks !== "object") {
        throw new TypeError("Managed debugger hooks must be an object.");
    }
    for (const name of [
        "isInstructionCheckEnabled",
        "pauseAtManagedInstruction",
        "pauseAtThrownException",
        "pauseAtUnhandledException",
        "completeException",
        "createAsyncContinuationContext"
    ]) {
        if (typeof hooks[name] !== "function") {
            throw new TypeError(`Managed debugger hook '${name}' must be a function.`);
        }
    }
    if (managedDebuggerHooks !== null && managedDebuggerHooks !== hooks) {
        throw new Error("Managed debugger hooks are already installed for this page.");
    }
    managedDebuggerHooks = Object.freeze(hooks);
    return managedDebuggerHooks;
}

export function createAsyncContinuationContext(ctx) {
    return managedDebuggerHooks === null
        ? { ...ctx }
        : managedDebuggerHooks.createAsyncContinuationContext(ctx);
}
