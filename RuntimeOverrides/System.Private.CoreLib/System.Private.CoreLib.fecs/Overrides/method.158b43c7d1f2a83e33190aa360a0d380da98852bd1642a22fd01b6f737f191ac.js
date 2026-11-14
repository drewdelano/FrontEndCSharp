import { initializeValueTaskFromTask } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Threading.Tasks.ValueTask`1[TResult]..ctor(System.Threading.Tasks.Task`1[TResult])'] = function (_ctx, valueTask, task) {
    initializeValueTaskFromTask(valueTask, task);
};
