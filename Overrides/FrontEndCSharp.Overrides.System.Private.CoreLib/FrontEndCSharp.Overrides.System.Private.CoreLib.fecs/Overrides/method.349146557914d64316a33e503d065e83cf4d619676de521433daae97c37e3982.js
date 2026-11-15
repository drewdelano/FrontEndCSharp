import { setAsyncValueTaskException } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.SetException(System.Exception)'] = function (_ctx, builder, exception) {
    setAsyncValueTaskException(builder, exception);
};
