import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task`1[TResult].GetAwaiter()'] = function (ctx, $this) {
    return wrap({ task: $this }, 'System.Runtime.CompilerServices.TaskAwaiter`1[TResult]');
};
