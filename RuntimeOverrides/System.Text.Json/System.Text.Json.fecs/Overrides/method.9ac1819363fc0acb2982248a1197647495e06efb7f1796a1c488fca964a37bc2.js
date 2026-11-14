import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Text.Json.JsonSerializerOptions.get_Converters()'] = function (ctx, $this) {
    return unwrap($this)._converters;
};
