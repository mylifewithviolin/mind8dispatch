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
            string mcodefilename=args.Length > 0 ? args[0] : "hellolevel7.mco";
            Dispatcher dispatcher =new();
            return dispatcher.Main(mcodefilename, args.Length > 0 ? args[1..] : []);
    
        }       
    }
}
