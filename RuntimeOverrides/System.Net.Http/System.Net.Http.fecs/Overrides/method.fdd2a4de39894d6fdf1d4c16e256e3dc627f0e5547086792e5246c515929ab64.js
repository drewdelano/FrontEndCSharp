import {
    dictionaryGetEnumerator,
    dictionarySet,
    initializeDictionary
} from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

const enumerableStringType = {
    typeName: 'System.Collections.Generic.IEnumerable`1[T]',
    genericParams: { T: 'System.String' }
};
const dictionaryTypeArguments = {
    TKey: 'System.String',
    TValue: enumerableStringType
};

globalThis.DotNet.Methods['System.Net.Http.Headers.HttpHeaders.GetEnumerator()'] = function (ctx, $this) {
    const dictionary = wrap(
        {},
        'System.Collections.Generic.Dictionary`2[TKey, TValue]',
        dictionaryTypeArguments);
    initializeDictionary(ctx, dictionary);

    const headers = unwrap($this).$fecsHeaders;
    if (headers instanceof Map) {
        for (const [name, values] of headers) {
            dictionarySet(
                ctx,
                dictionary,
                wrap(name, 'System.String'),
                wrap(values.map(value => wrap(value, 'System.String')), 'System.String[]'));
        }
    }
    return dictionaryGetEnumerator(ctx, dictionary);
};
