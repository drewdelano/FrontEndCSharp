import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.AggregateException.get_InnerExceptions()'] = function (ctx, $this) {
    const aggregate = unwrap($this);
    if (aggregate._rocView === null) {
        aggregate._rocView = wrap({
            list: aggregate._innerExceptions
        }, 'System.Collections.ObjectModel.ReadOnlyCollection`1[T]', {
            T: 'System.Exception'
        });
    }
    return aggregate._rocView;
};
