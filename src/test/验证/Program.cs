using static 验证.OperationA;

namespace 验证
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            Person person = new Person("aaa");

            //OperationA operationA = new OperationA();
            //OperationB operationB = new OperationB(ref operationA.P);

            //operationA.Msg();
            //operationB.Msg();

            //operationA.ReInit();
            //operationA.Msg();
            //operationB.Msg();
        }
    }

    public class OperationA
    {
        public Person P;
        public OperationA()
        {
            P = new Person() { Name = "ZS", Id = 1 };
        }

        public void Msg()
        {
            Console.WriteLine($"{P.Name}  {P.Id}");
        }

        public void ReInit()
        {

            P = new Person() { Name = "AAA", Id = 66 };
        }

        public class OperationB
        {
            public Person P;
            public OperationB(ref Person person)
            {
                P = person;
            }
            public void Msg()
            {
                Console.WriteLine($"{P.Name}  {P.Id}");
            }
        }

        public class Person : SuperPerson
        {
            public Person()
            {
            }

            public Person(string name) : base(name)
            {
                Console.WriteLine("this is run person");
            }
 
            
        }

        public abstract class SuperPerson
        {
            public string Name { get; set; }
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public SuperPerson( )
            {
                 
            }

            public SuperPerson(string name)
            {
                this.Name = name;
                Console.WriteLine("this is run supper");
            }
        }
    }
}
