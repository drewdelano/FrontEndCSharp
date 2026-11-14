import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.ComponentModel.DataAnnotations.ValidationContext..ctor(System.Object)'] = function (ctx, $this, instance) {
    const context = unwrap($this);
    context['<ObjectInstance>k__BackingField'] = instance;
    context['<MemberName>k__BackingField'] = wrap(null, 'System.String');
    context._displayName = wrap(null, 'System.String');
};
