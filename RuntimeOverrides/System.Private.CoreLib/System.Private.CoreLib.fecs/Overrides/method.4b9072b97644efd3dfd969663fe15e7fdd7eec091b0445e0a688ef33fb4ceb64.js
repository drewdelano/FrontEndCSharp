import { initializeValueTaskFromSource } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Threading.Tasks.ValueTask..ctor(System.Threading.Tasks.Sources.IValueTaskSource, System.Int16)'] = function (_ctx, valueTask, source, token) {
    initializeValueTaskFromSource(valueTask, source, token);
};
