using FrontEndCSharp.Fecscn.Commands;

return await FecscnCommandHost.RunAsync(args, Directory.GetCurrentDirectory(), Console.Out, Console.Error);
