import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Newtonsoft.Json.JsonSerializerSettings.get_Converters()'] = function (ctx, $this) {
    return unwrap($this)['<Converters>k__BackingField'];
};
