import { createManagedException } from "../../managed-exceptions.js";
import { throwDotNetException, unwrap, wrap } from "../../msil-type-helpers.js";
import {
    getTaskCompletionSource as getRuntimeTaskCompletionSource,
    JavaScriptTaskException,
    registerTaskCompletionSource,
    tryGetTaskCompletionSource as tryGetRuntimeTaskCompletionSource,
    waitForTask as waitForRuntimeTask
} from "../../task-runtime.js";

export { JavaScriptTaskException };

const pendingStatus = "pending";
const succeededStatus = "succeeded";
const faultedStatus = "faulted";
const canceledStatus = "canceled";

export const JavaScriptTaskStatus = Object.freeze({
    pending: pendingStatus,
    succeeded: succeededStatus,
    faulted: faultedStatus,
    canceled: canceledStatus
});

export class JavaScriptTaskCompletionSource {
    #continuations = [];
    #exception = null;
    #exceptions = [];
    #result = null;
    #status = pendingStatus;
    #task;

    constructor() {
        this.#task = Object.freeze({});
        registerTaskCompletionSource(this.#task, this);
    }

    get task() {
        return this.#task;
    }

    get status() {
        return this.#status;
    }

    get isCompleted() {
        return this.#status !== pendingStatus;
    }

    get isCompletedSuccessfully() {
        return this.#status === succeededStatus;
    }

    get isFaulted() {
        return this.#status === faultedStatus;
    }

    get isCanceled() {
        return this.#status === canceledStatus;
    }

    get result() {
        return this.#result;
    }

    get exception() {
        return this.#exception;
    }

    get exceptions() {
        return this.#exceptions;
    }

    registerContinuation(continuation, executeSynchronously = false) {
        if (typeof continuation !== "function") {
            throw new TypeError("A Task continuation must be a function.");
        }

        if (this.isCompleted) {
            if (executeSynchronously) {
                continuation();
            } else {
                queueMicrotask(continuation);
            }
            return;
        }

        this.#continuations.push({ continuation, executeSynchronously });
    }

    setResult(result) {
        if (!this.trySetResult(result)) {
            throw new Error("The Task is already completed.");
        }
    }

    trySetResult(result) {
        return this.#tryComplete(succeededStatus, result, null);
    }

    setException(exception) {
        if (!this.trySetException(exception)) {
            throw new Error("The Task is already completed.");
        }
    }

    trySetException(exception) {
        requireException(exception);
        return this.#tryComplete(faultedStatus, null, exception, [exception]);
    }

    setExceptions(exceptions) {
        if (!this.trySetExceptions(exceptions)) {
            throw new Error("The Task is already completed.");
        }
    }

    trySetExceptions(exceptions) {
        if (!Array.isArray(exceptions) || exceptions.length === 0) {
            throw new TypeError("A faulted Task requires at least one wrapped .NET exception.");
        }
        exceptions.forEach(requireException);
        return this.#tryComplete(faultedStatus, null, exceptions[0], [...exceptions]);
    }

    setCanceled(exception) {
        if (!this.trySetCanceled(exception)) {
            throw new Error("The Task is already completed.");
        }
    }

    trySetCanceled(exception) {
        requireException(exception);
        return this.#tryComplete(canceledStatus, null, exception);
    }

    #tryComplete(status, result, exception, exceptions = []) {
        if (this.isCompleted) {
            return false;
        }

        this.#status = status;
        this.#result = result;
        this.#exception = exception;
        this.#exceptions = exceptions;

        const continuations = this.#continuations;
        this.#continuations = [];
        for (const entry of continuations) {
            if (entry.executeSynchronously) {
                entry.continuation();
            } else {
                queueMicrotask(entry.continuation);
            }
        }

        return true;
    }
}

export function getTaskCompletionSource(task) {
    return getRuntimeTaskCompletionSource(task);
}

export function tryGetTaskCompletionSource(task) {
    return tryGetRuntimeTaskCompletionSource(task);
}

export function createCompletedTask(typeName, result = null, genericTypeMap = null) {
    const source = new JavaScriptTaskCompletionSource();
    source.setResult(result);
    return wrap(source.task, typeName, genericTypeMap);
}

export function createFaultedTask(typeName, exception, genericTypeMap = null) {
    const source = new JavaScriptTaskCompletionSource();
    source.setException(exception);
    return wrap(source.task, typeName, genericTypeMap);
}

export function setTaskExceptionOrCancellation(source, exception) {
    if (exception?.t === 'System.OperationCanceledException'
        || exception?.t === 'System.Threading.Tasks.TaskCanceledException') {
        source.setCanceled(exception);
        return;
    }
    source.setException(exception);
}

export function createAggregateException(exceptions) {
    const innerExceptions = Array.isArray(exceptions) ? exceptions : [exceptions];
    const innerMessages = innerExceptions
        .map(exception => unwrap(unwrap(exception)?.['_message']))
        .filter(message => message !== null && message !== undefined);
    const message = 'One or more errors occurred.'
        + innerMessages.map(innerMessage => ` (${innerMessage})`).join('');

    return createManagedException(
        'System.AggregateException',
        message,
        innerExceptions[0],
        {
            _innerExceptions: wrap(innerExceptions, 'System.Exception[]'),
            _rocView: null
        });
}

export function getTaskResult(ctx, task) {
    const source = getTaskCompletionSource(unwrap(task));
    if (!source.isCompleted) {
        throw new Error('Task<TResult>.Result cannot synchronously block the JavaScript event loop.');
    }
    if (source.isFaulted || source.isCanceled) {
        const exceptions = source.isFaulted ? source.exceptions : [source.exception];
        throwDotNetException(ctx, createAggregateException(exceptions));
    }
    return source.result;
}

export function createWhenAllTask(tasks, taskTypeName, resultArrayTypeName = null) {
    const taskList = requireTaskArray(tasks);
    const source = new JavaScriptTaskCompletionSource();
    if (taskList.length === 0) {
        source.setResult(resultArrayTypeName === null ? null : wrap([], resultArrayTypeName));
        return wrap(source.task, taskTypeName);
    }

    let remaining = taskList.length;
    const exceptions = [];
    let firstCancellation = null;
    const results = new Array(taskList.length);
    const taskCompleted = (taskSource, index) => {
        if (taskSource.isFaulted) {
            exceptions.push(...taskSource.exceptions);
        } else if (taskSource.isCanceled && firstCancellation === null) {
            firstCancellation = taskSource.exception;
        } else if (taskSource.isCompletedSuccessfully) {
            results[index] = taskSource.result;
        }

        remaining--;
        if (remaining !== 0) {
            return;
        }
        if (exceptions.length > 0) {
            source.setExceptions(exceptions);
        } else if (firstCancellation !== null) {
            source.setCanceled(firstCancellation);
        } else {
            source.setResult(resultArrayTypeName === null ? null : wrap(results, resultArrayTypeName));
        }
    };

    taskList.forEach((task, index) => {
        const taskSource = getTaskCompletionSource(unwrap(task));
        if (taskSource.isCompleted) {
            taskCompleted(taskSource, index);
        } else {
            taskSource.registerContinuation(() => taskCompleted(taskSource, index));
        }
    });
    return wrap(source.task, taskTypeName);
}

export function createWhenAnyTask(tasks, taskTypeName) {
    const taskList = requireTaskArray(tasks);
    if (taskList.length === 0) {
        throw new TypeError('Task.WhenAny requires at least one Task.');
    }

    const source = new JavaScriptTaskCompletionSource();
    const complete = task => source.trySetResult(task);
    for (const task of taskList) {
        const taskSource = getTaskCompletionSource(unwrap(task));
        if (taskSource.isCompleted) {
            complete(task);
            break;
        }
        taskSource.registerContinuation(() => complete(task));
    }
    return wrap(source.task, taskTypeName);
}

function requireTaskArray(tasks) {
    const taskList = unwrap(tasks);
    if (!Array.isArray(taskList)) {
        throw new TypeError('A wrapped Task array is required.');
    }
    if (taskList.some(task => task === null)) {
        throw new TypeError('A Task array cannot contain null.');
    }
    return taskList;
}

export function waitForTask(task) {
    return waitForRuntimeTask(task);
}

export function registerAwaiterContinuation(awaiter, continuation, ctx = null) {
    if (awaiter === null || typeof awaiter !== "object" || typeof awaiter.t !== "string") {
        throw new TypeError("An awaiter must be a wrapped .NET value.");
    }
    if (typeof continuation !== "function") {
        throw new TypeError("An awaiter continuation must be a function.");
    }

    if (awaiter.t === "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter") {
        queueMicrotask(continuation);
        return;
    }

    const isTaskAwaiter = awaiter.t === "System.Runtime.CompilerServices.TaskAwaiter"
        || (awaiter.t.startsWith("System.Runtime.CompilerServices.TaskAwaiter`1[")
            && awaiter.t.endsWith("]"));
    const isConfiguredTaskAwaiter = awaiter.t === "System.Runtime.CompilerServices.ConfiguredTaskAwaitable+ConfiguredTaskAwaiter"
        || awaiter.t.startsWith("System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter[");
    const isValueTaskAwaiter = awaiter.t === "System.Runtime.CompilerServices.ValueTaskAwaiter"
        || (awaiter.t.startsWith("System.Runtime.CompilerServices.ValueTaskAwaiter`1[")
            && awaiter.t.endsWith("]"));
    if (isTaskAwaiter || isConfiguredTaskAwaiter || isValueTaskAwaiter) {
        const taskAwaiter = awaiter.v;
        if (taskAwaiter === null || typeof taskAwaiter !== "object") {
            throw new TypeError("A task awaiter must reference its Task.");
        }

        if ("source" in taskAwaiter) {
            if (ctx === null) {
                throw new Error("A source-backed ValueTask awaiter requires a managed execution context.");
            }
            invokeValueTaskSourceMethod(
                ctx,
                taskAwaiter.source,
                taskAwaiter.generic,
                "OnCompleted",
                [
                    wrap(continuation, "System.Action`1[System.Object]"),
                    null,
                    wrap(taskAwaiter.token, "System.Int16"),
                    wrap(0, "System.Threading.Tasks.Sources.ValueTaskSourceOnCompletedFlags")
                ]);
            return;
        }
        if (taskAwaiter.completed === true) {
            queueMicrotask(continuation);
            return;
        }
        if (!("task" in taskAwaiter)) {
            throw new TypeError("A task awaiter must reference its Task.");
        }

        const task = taskAwaiter.task;
        const unwrappedTask = task !== null && typeof task === "object" && "v" in task
            ? task.v
            : task;
        getTaskCompletionSource(unwrappedTask).registerContinuation(continuation);
        return;
    }

    throw new Error(`Awaiter type '${awaiter.t}' is not supported by the JavaScript scheduler.`);
}

export function registerSafeAwaiterContinuation(ctx, awaiter, continuation) {
    try {
        registerAwaiterContinuation(awaiter, continuation, ctx);
        return;
    } catch (error) {
        if (!(error instanceof Error)
            || !error.message.endsWith("is not supported by the JavaScript scheduler.")) {
            throw error;
        }
    }

    const onCompleted = globalThis.DotNet?.Methods?.[`${awaiter.t}.OnCompleted(System.Action)`];
    if (typeof onCompleted !== "function") {
        throw new Error(`Awaiter type '${awaiter.t}' does not provide OnCompleted(System.Action).`);
    }

    onCompleted(ctx, awaiter, wrap(continuation, "System.Action"));
}

export function invokeValueTaskSourceMethod(ctx, source, generic, methodName, args) {
    const prefix = `${source?.t}.`;
    const interfaceMarker = generic
        ? ".System.Threading.Tasks.Sources.IValueTaskSource`1["
        : ".System.Threading.Tasks.Sources.IValueTaskSource.";
    const suffixStart = `.${methodName}(`;
    const candidates = Object.entries(globalThis.DotNet?.Methods ?? {})
        .filter(([name, method]) => typeof method === "function"
            && name.startsWith(prefix)
            && name.includes(interfaceMarker)
            && name.includes(suffixStart))
        .sort(([left], [right]) => left.localeCompare(right));
    const publicMethod = globalThis.DotNet?.Methods?.[`${source?.t}.${methodName}(${args
        .map(argument => argument?.t ?? "System.Object")
        .join(", ")})`];
    const method = candidates[0]?.[1] ?? publicMethod;
    if (typeof method !== "function") {
        const available = Object.keys(globalThis.DotNet?.Methods ?? {})
            .filter(name => name.startsWith(prefix))
            .sort()
            .join(", ");
        throw new Error(`ValueTask source method '${source?.t}.${methodName}' was not emitted. Available methods: ${available}`);
    }
    return method(ctx, source, ...args);
}

function requireException(exception) {
    if (exception === null || typeof exception !== "object") {
        throw new TypeError("A faulted or canceled Task requires a wrapped .NET exception.");
    }
}
