import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.GetAwaiter()'] = function (ctx, $this) {
    return wrap({ task: $this }, "System.Runtime.CompilerServices.TaskAwaiter");
};
