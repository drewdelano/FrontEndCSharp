import { throwDotNetException, unwrap } from '../msil-type-helpers.js';
globalThis.DotNet.Methods['System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()'] = function (ctx, dispatchInfo) {
    throwDotNetException(ctx, unwrap(dispatchInfo).exception);
};
