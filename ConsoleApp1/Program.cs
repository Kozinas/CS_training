using System;

namespace MyApp
{
    internal class Program1
    {
        public int func1()
        {
            return 123;
        }

        public int summ(int a, int b)
        {
            int summ = 0;

            for (int i = 0; i < 10; i++)
            {
                summ += a + b;

                Console.WriteLine(summ);
            }

            return summ;
        }

        public void except(dynamic a, dynamic b)
        {
            try
            {
                if (a != b)
                    throw new InvalidOperationException("Values are not equal");
                
                Console.WriteLine("YES");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);

                throw;
            }
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            var program = new Program1();

            Console.WriteLine(program.func1());

            program.summ(1, 9);

            program.except("a", "b");
        }
    }
}

