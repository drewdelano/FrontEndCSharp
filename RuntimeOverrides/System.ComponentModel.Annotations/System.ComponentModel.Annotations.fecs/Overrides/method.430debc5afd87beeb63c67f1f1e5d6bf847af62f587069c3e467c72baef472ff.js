import { validateValue } from './method.d1a7884ad63b8d433d6bb63dcb84c4923e170e7a93bac3f4fce39fc6db12d7ac.js';
import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.ComponentModel.DataAnnotations.Validator.TryValidateValue(System.Object, System.ComponentModel.DataAnnotations.ValidationContext, System.Collections.Generic.ICollection`1[T], System.Collections.Generic.IEnumerable`1[T])'] = function (ctx, value, validationContext, validationResults, validationAttributes) {
    return wrap(validateValue(ctx, value, validationContext, validationResults, validationAttributes), 'System.Boolean');
};
