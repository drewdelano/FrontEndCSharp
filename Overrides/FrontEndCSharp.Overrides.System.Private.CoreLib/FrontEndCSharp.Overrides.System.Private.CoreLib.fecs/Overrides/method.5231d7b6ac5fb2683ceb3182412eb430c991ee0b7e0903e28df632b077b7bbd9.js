import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].SetResult(TResult)'] = function (ctx, $this, result) {
    const taskMethodBuilder = unwrap($this);
    taskMethodBuilder.completionSource.setResult(result);
};
