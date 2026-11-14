import { initializeValueTaskFromTask } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Threading.Tasks.ValueTask..ctor(System.Threading.Tasks.Task)'] = function (_ctx, valueTask, task) {
    initializeValueTaskFromTask(valueTask, task);
};
