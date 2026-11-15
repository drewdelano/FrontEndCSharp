import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.AggregateException.get_Message()'] = function (ctx, $this) {
    return unwrap($this)._message;
};
