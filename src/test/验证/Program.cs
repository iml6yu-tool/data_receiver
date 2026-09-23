

using static OperationA;

namespace 验证
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
             
            //Person person = new Person("aaa");

            //OperationA operationA = new OperationA();
            //OperationB operationB = new OperationB(ref operationA.P);

            //operationA.Msg();
            //operationB.Msg();

            //operationA.ReInit();
            //operationA.Msg();
            //operationB.Msg();

            #region Test DeepCopy
            var p = new Person("aaa") { Id = 1, Title = "ttt", Description = "ddd", F1 = 1 };
            var cp = p.DeepCopy();
            cp.Name = "bbb";
            Console.WriteLine($"p.Name={p.Name},cp.Name={cp.Name}");
            var a = "xxx";
            var b = a;
            b = "yyy";
            Console.WriteLine($"a={a},b={b}");
            #endregion
            Console.ReadLine();
        }
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
        public Person DeepCopy()
        {
            return new Person()
            {
                Name = this.Name,
                Id = this.Id,
                Title = this.Title,
                Description = this.Description
            };
        }

    }

    public abstract class SuperPerson
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public object F1 { get; set; }
        public SuperPerson()
        {

        }

        public SuperPerson(string name)
        {
            this.Name = name;
            Console.WriteLine("this is run supper");
        }
    }
}
 