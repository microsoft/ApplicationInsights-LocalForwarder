using System;

namespace Microsoft.LocalForwarder.ConsoleHost
{
    class SomeClass
    {
        public void SomeMethod()
        {
            Console.WriteLine("Some method");
        }

        public void SomeGenericMethod<T>()
        {
            Console.WriteLine("Some generic method");
        }

        protected static void LoadInstances<T>(int definition, T instances)
        {
            return;
        }
    }
}
