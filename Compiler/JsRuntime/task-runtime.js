const taskCompletionSources = new WeakMap();

export function registerTaskCompletionSource(task, source) {
    if (task === null || typeof task !== "object" || source === null || typeof source !== "object") {
        throw new TypeError("A Task and its completion source must be objects.");
    }
    if (taskCompletionSources.has(task)) {
        throw new Error("The Task already has a completion source.");
    }
    taskCompletionSources.set(task, source);
}

export function getTaskCompletionSource(task) {
    if (task === null || typeof task !== "object") {
        throw new TypeError("The Task must be an object owned by a JavaScriptTaskCompletionSource.");
    }
    if (!taskCompletionSources.has(task)) {
        throw new Error("The Task is not owned by a JavaScriptTaskCompletionSource.");
    }
    return taskCompletionSources.get(task);
}

export function tryGetTaskCompletionSource(task) {
    return task !== null && typeof task === "object" && taskCompletionSources.has(task)
        ? taskCompletionSources.get(task)
        : null;
}

export function waitForTask(task) {
    const source = getTaskCompletionSource(task);
    return new Promise((resolve, reject) => {
        source.registerContinuation(() => {
            if (source.isCompletedSuccessfully) {
                resolve(source.result);
                return;
            }
            if (source.isFaulted) {
                globalThis.__FrontEndCSharpDebugger
                    ?.reportUnhandledTaskException?.(source.exception);
            }
            reject(new JavaScriptTaskException(source.status, source.exception));
        });
    });
}

export class JavaScriptTaskException extends Error {
    constructor(status, dotNetException) {
        super(`The .NET Task completed with status '${status}'.`, { cause: dotNetException });
        this.name = "JavaScriptTaskException";
        this.status = status;
        this.dotNetException = dotNetException;
    }
}
