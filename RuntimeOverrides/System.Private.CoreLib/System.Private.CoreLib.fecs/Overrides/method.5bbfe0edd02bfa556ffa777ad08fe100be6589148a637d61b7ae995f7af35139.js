import { setNonGenericAsyncValueTaskResult } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.SetResult()'] = function (_ctx, builder) {
    setNonGenericAsyncValueTaskResult(builder);
};
