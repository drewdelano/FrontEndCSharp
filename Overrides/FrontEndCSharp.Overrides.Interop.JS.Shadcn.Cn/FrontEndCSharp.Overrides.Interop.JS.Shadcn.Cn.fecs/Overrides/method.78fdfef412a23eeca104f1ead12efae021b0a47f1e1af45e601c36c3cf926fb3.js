import { cn } from 'cn';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Shadcn.Cn.CnImports.MergeClassNames(System.String[])'] = function (ctx, values) {
    const nativeValues = unwrap(values).map(value => unwrap(value));
    return wrap(cn(...nativeValues), 'System.String');
};
