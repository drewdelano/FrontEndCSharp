import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.HttpHeaderValueCollection`1[T].Add(T)'] = function (ctx, $this, item) {
    const collection = unwrap($this);
    const owner = collection.$fecsHeaderOwner;
    const name = collection.$fecsHeaderName;
    const itemValue = unwrap(item);
    if (!owner || typeof name !== 'string') {
        throw new Error('The browser HTTP header collection is not associated with a request.');
    }
    if (itemValue === null || itemValue === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Header value cannot be null.');
    }

    owner.$fecsHeaders ??= new Map();
    const values = owner.$fecsHeaders.get(name) ?? [];
    values.push(itemValue.$fecsHeaderValue);
    owner.$fecsHeaders.set(name, values);
};
