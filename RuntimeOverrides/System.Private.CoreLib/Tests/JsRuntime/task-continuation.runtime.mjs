import assert from 'node:assert/strict';
import test from 'node:test';

import {
    createContinuationTask,
    defaultTaskScheduler,
    noCancellationToken,
    noContinuationOptions
} from '../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/task-continuation.js';
import { createCompletedTask, getTaskCompletionSource } from '../../JsRuntime/runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';
import { unwrap, wrap } from '../../JsRuntime/msil-type-helpers.js';

const ctx = { stackTrace: '' };

test('ExecuteSynchronously runs an eligible completed-task continuation inline', () => {
    const antecedent = createCompletedTask('System.Threading.Tasks.Task');
    let invoked = false;
    const continuation = wrap(() => {
        invoked = true;
    }, 'System.Action`1[System.Threading.Tasks.Task]');

    const task = createContinuationTask(
        ctx,
        antecedent,
        continuation,
        [antecedent],
        noCancellationToken,
        wrap(524288, 'System.Threading.Tasks.TaskContinuationOptions'),
        defaultTaskScheduler,
        'System.Threading.Tasks.Task',
        false,
        'continuationAction');

    assert.equal(invoked, true);
    assert.equal(getTaskCompletionSource(unwrap(task)).isCompletedSuccessfully, true);
});

test('status filters cancel a continuation without invoking its delegate', () => {
    const antecedent = createCompletedTask('System.Threading.Tasks.Task');
    let invoked = false;
    const continuation = wrap(() => {
        invoked = true;
    }, 'System.Action`1[System.Threading.Tasks.Task]');

    const task = createContinuationTask(
        ctx,
        antecedent,
        continuation,
        [antecedent],
        noCancellationToken,
        wrap(65536, 'System.Threading.Tasks.TaskContinuationOptions'),
        defaultTaskScheduler,
        'System.Threading.Tasks.Task',
        false,
        'continuationAction');

    assert.equal(invoked, false);
    assert.equal(getTaskCompletionSource(unwrap(task)).isCanceled, true);
});

test('pre-cancellation prevents continuation delegate invocation', () => {
    const antecedent = createCompletedTask('System.Threading.Tasks.Task');
    let invoked = false;
    const continuation = wrap(() => {
        invoked = true;
    }, 'System.Action`1[System.Threading.Tasks.Task]');

    const task = createContinuationTask(
        ctx,
        antecedent,
        continuation,
        [antecedent],
        wrap({ $isCancellationRequested: true }, 'System.Threading.CancellationToken'),
        noContinuationOptions,
        defaultTaskScheduler,
        'System.Threading.Tasks.Task',
        false,
        'continuationAction');

    assert.equal(invoked, false);
    assert.equal(getTaskCompletionSource(unwrap(task)).isCanceled, true);
});
