import { getBuilderValueTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1[TResult].get_Task()'] = function (ctx, builder) {
    return getBuilderValueTask(ctx, builder);
};
