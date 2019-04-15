using System;

namespace SuperSharpScriptHeaderGenerator
{
    class Program
    {
        private static SharpScriptGenerator _scriptGenerator;

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine($"Project: {args[0]}");
                _scriptGenerator = new SharpScriptGenerator(args[0]);
                _scriptGenerator.MessageOut += _scriptGenerator_MessageOut;
                _scriptGenerator.CopyProjectFiles();
                _scriptGenerator.CreatePowershellScript();
                _scriptGenerator.CreateConfigurationFile();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine(string.Empty);
                Console.WriteLine("Press any key to continue...");
                return;
            }
            Console.WriteLine(string.Empty);
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void _scriptGenerator_MessageOut(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
