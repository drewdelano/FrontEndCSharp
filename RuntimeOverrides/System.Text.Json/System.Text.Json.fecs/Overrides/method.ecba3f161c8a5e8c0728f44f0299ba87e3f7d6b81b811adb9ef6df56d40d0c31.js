import { initializeList } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Text.Json.JsonSerializerOptions..ctor()'] = function (ctx, $this) {
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
};
