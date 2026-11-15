import { setNonGenericAsyncValueTaskResult } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.SetResult()'] = function (_ctx, builder) {
    setNonGenericAsyncValueTaskResult(builder);
};
