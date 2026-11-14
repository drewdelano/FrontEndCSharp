import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Collections.ObjectModel.ReadOnlyCollection`1[T].get_Item(System.Int32)'] = function (ctx, $this, index) {
    return unwrap(unwrap($this).list)[unwrap(index)];
};
