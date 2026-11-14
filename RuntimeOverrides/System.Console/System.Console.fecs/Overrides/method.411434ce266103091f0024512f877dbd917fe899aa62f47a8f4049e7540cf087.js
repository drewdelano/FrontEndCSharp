import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Console.WriteLine(System.Single)'] = function(ctx, float) {
    console.log(unwrap(float));
};
