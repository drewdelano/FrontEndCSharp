import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Console.WriteLine(System.Double)'] = function(ctx, dbl) {
  console.log(unwrap(dbl));
};
