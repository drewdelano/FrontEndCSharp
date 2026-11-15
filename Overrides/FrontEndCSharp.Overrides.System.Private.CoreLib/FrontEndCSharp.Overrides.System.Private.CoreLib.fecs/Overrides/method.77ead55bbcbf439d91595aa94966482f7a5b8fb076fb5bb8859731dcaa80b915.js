import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.ArgumentException.get_Message()'] = function (ctx, $this) {
    return unwrap($this)._message;
};
