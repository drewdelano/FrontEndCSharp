import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetResult()'] = function (ctx, $this) {
    const taskMethodBuilder = unwrap($this);
    taskMethodBuilder.completionSource.setResult(null);
};
