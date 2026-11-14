import { wrap } from '../msil-type-helpers.js';

const currentThread = wrap({}, 'System.Threading.Thread');

globalThis.DotNet.Methods['System.Threading.Thread.get_CurrentThread()'] = function () {
    return currentThread;
};
