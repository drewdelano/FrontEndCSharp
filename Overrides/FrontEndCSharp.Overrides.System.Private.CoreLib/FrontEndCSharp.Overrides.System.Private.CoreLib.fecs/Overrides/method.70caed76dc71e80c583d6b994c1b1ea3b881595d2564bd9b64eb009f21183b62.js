import { wrap } from '../msil-type-helpers.js';
globalThis.DotNet.Methods['System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(System.Exception)'] = function (_ctx, exception) {
    return wrap({ exception }, 'System.Runtime.ExceptionServices.ExceptionDispatchInfo');
};
