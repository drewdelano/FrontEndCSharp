import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpMethod..ctor(System.String, System.Int32)'] = function (ctx, $this, method, http3Index) {
    const instance = unwrap($this);
    instance._method = method;
    instance._http3Index = http3Index;
    instance._hashcode = wrap(0, 'System.Int32');
};
