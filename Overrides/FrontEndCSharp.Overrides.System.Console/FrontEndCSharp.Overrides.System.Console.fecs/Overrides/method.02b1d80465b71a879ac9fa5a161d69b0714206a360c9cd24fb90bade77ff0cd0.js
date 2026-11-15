import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Console.WriteLine(System.Int32)'] = function(ctx, num) {
  console.log(unwrap(num));
};
