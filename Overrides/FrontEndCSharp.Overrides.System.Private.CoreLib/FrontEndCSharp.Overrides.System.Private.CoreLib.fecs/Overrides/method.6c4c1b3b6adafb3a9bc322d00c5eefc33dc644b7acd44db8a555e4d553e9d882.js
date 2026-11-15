import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Threading.CancellationToken.get_None()'] = function () {
    return wrap({ $isCancellationRequested: false }, 'System.Threading.CancellationToken');
};
