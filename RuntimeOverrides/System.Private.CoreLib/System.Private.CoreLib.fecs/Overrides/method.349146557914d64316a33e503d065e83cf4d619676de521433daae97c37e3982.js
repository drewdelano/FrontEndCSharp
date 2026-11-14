import { setAsyncValueTaskException } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.SetException(System.Exception)'] = function (_ctx, builder, exception) {
    setAsyncValueTaskException(builder, exception);
};
