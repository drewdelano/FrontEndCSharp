import { ExecuteMSIL } from '../msil.js';

globalThis.DotNet.Methods["System.Console.WriteLine(System.Char)"] = function(ctx, value) {
  console.log(value.v);
};
