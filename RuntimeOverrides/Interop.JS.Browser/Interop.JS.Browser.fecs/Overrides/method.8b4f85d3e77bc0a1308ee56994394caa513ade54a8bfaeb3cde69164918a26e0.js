import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.get_Value()'] = function (ctx, element) {
    const value = unwrap(element).value;
    if (typeof value !== 'string') {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'The element does not expose a string value.');
    }
    return wrap(value, 'System.String');
};
