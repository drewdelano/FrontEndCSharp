import { initializeValueTaskFromSource } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Threading.Tasks.ValueTask`1[TResult]..ctor(System.Threading.Tasks.Sources.IValueTaskSource`1[TResult], System.Int16)'] = function (_ctx, valueTask, source, token) {
    initializeValueTaskFromSource(valueTask, source, token);
};
