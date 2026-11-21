using System.Dynamic;

namespace Mind8Kernel
{
    /// <summary>Mind8ディスパッチャ</summary>
    public partial class Mind8Dispatcher
    {
        /// <summary>メイン</summary>
        /// <param name="args">引数</param>
        static int Main(string[] args)
        {
            bool traceEnabled = args.Contains("-D");
            string[] programArgs = args.Where(argument => argument != "-D").ToArray();
            string mcodefilename=programArgs.Length > 0 ? programArgs[0] : "hellolevel7.mco";
            Dispatcher dispatcher =new() { TraceEnabled = traceEnabled };
            return dispatcher.Main(mcodefilename, programArgs.Length > 0 ? programArgs[1..] : []);
    
        }       
    }
}
