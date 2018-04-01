using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AltThread;

namespace Example
{
    class Example
    {
        static void Main(string[] args)
        {
            ThreadController x = new ThreadController(
                new ThreadControllerConfig() { Name="test" }
            );
        }
    }
}
