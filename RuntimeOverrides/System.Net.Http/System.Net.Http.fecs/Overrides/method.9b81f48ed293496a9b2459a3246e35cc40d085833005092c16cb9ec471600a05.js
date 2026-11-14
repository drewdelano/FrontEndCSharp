import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Net.Http.FormUrlEncodedContent..ctor(System.Collections.Generic.IEnumerable`1[T])'] = function (ctx) {
    throwManagedException(
        ctx,
        'System.NotImplementedException',
        'FormUrlEncodedContent is not implemented by the browser HTTP companion.');
};
