import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Threading.CancellationToken..ctor(System.Boolean)'] = function (ctx, $this, canceled) {
    unwrap($this).$isCancellationRequested = unwrap(canceled) !== 0;
};
