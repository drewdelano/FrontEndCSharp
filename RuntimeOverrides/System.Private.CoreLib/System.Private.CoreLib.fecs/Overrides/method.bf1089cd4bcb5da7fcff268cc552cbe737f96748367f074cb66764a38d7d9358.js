import { setAsyncValueTaskResult } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1[TResult].SetResult(TResult)'] = function (_ctx, builder, result) {
    setAsyncValueTaskResult(builder, result);
};
