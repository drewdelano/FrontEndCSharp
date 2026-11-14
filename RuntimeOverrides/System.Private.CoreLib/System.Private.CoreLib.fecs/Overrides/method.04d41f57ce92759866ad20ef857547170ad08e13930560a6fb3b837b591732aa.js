import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Collections.ObjectModel.ReadOnlyCollection`1[T].get_Count()'] = function (ctx, $this) {
    return wrap(unwrap(unwrap($this).list).length, 'System.Int32');
};
