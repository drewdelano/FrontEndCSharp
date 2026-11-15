import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Threading.Monitor.Enter(System.Object, System.Boolean&)'] = function (_ctx, _syncRoot, lockTaken) {
    lockTaken.value = wrap(true, 'System.Boolean');
};
