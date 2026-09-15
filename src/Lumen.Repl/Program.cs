using Lumen.Repl;

using CancellationTokenSource scriptCancellation = new();
Repl? repl = null;

// Ctrl+C 只中斷目前的求值，不結束 process；非互動模式則讓 Cli 回 130。
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (repl is null)
    {
        scriptCancellation.Cancel();
    }
    else
    {
        repl.Interrupt();
    }
};

if (args.Length == 0)
{
    repl = new Repl(Console.In, Console.Out);
    repl.Run();
    return Cli.ExitOk;
}

return Cli.Run(args, Console.Out, Console.Error, scriptCancellation.Token);
