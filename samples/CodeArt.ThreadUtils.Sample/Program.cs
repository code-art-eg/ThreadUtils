using System.Threading.Tasks;

namespace CodeArt.ThreadUtils.Sample
{
    internal class Program
    {
        private static async Task Main()
        {
            await ReaderWriterSample.RunAsync();
        }

        
    }
}
