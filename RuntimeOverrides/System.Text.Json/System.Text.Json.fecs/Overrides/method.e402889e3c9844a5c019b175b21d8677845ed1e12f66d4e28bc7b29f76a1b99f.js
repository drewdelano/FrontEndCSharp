import { throwManagedException } from '../managed-exceptions.js';
import { initializeList } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Text.Json.JsonSerializerOptions..ctor(System.Text.Json.JsonSerializerDefaults)'] = function (ctx, $this, defaults) {
    const options = unwrap($this);
    const converters = wrap(
        {},
        'System.Collections.Generic.List`1[T]',
        { T: 'System.Text.Json.Serialization.JsonConverter' });
    initializeList(ctx, converters);
    options._converters = converters;
    options._defaultBufferSize = wrap(16384, 'System.Int32');
    options['<EffectiveMaxDepth>k__BackingField'] = wrap(64, 'System.Int32');
    options._readCommentHandling = wrap(0, 'System.Text.Json.JsonCommentHandling');
    options._defaultIgnoreCondition = wrap(0, 'System.Text.Json.Serialization.JsonIgnoreCondition');
    options._numberHandling = wrap(0, 'System.Text.Json.Serialization.JsonNumberHandling');
    options._preferredObjectCreationHandling = wrap(0, 'System.Text.Json.Serialization.JsonObjectCreationHandling');
    options._unknownTypeHandling = wrap(0, 'System.Text.Json.Serialization.JsonUnknownTypeHandling');
    options._unmappedMemberHandling = wrap(0, 'System.Text.Json.Serialization.JsonUnmappedMemberHandling');

    const value = unwrap(defaults);
    if (value === 0) return;
    if (value !== 1) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Specified argument was out of the range of valid values. (Parameter \'defaults\')');
    }
    options._propertyNameCaseInsensitive = wrap(true, 'System.Boolean');
    options._jsonPropertyNamingPolicy = wrap({}, 'System.Text.Json.JsonCamelCaseNamingPolicy');
    options._numberHandling = wrap(1, 'System.Text.Json.Serialization.JsonNumberHandling');
};
