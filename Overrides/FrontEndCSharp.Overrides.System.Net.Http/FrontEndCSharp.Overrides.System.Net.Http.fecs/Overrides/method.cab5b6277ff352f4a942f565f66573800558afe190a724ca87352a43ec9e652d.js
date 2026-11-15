import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpRequestMessage.set_Content(System.Net.Http.HttpContent)'] = function (ctx, $this, content) {
    unwrap($this)._content = content;
};
